namespace StudyLiveAssistant.Core;

public sealed class StudySessionService(ITaskRepository repository, IClock clock) : IStudySessionService
{
    private readonly List<StudyTask> _tasks = [];
    private int _currentIndex = -1;
    private DateTimeOffset _lastTick;
    private StudySession? _session;

    public IReadOnlyList<StudyTask> Tasks => _tasks;
    public StudyTask? CurrentTask => _currentIndex >= 0 && _currentIndex < _tasks.Count ? _tasks[_currentIndex] : null;
    public StudyTask? NextTask => _currentIndex >= 0 && _currentIndex + 1 < _tasks.Count ? _tasks[_currentIndex + 1] : null;
    public bool IsRunning { get; private set; }
    public event EventHandler? StateChanged;

    public async Task LoadDateAsync(DateOnly date, Guid? preferredTaskId = null)
    {
        await PauseAsync("change-date");
        _tasks.Clear();
        _tasks.AddRange(TaskRules.Sort(await repository.GetTasksAsync(date)));
        _currentIndex = preferredTaskId is { } id ? _tasks.FindIndex(t => t.Id == id) : -1;
        if (_currentIndex < 0) _currentIndex = _tasks.FindIndex(t => t.Status != StudyTaskStatus.Completed);
        if (_currentIndex < 0 && _tasks.Count > 0) _currentIndex = 0;
        OnStateChanged();
    }

    public Task StartAsync()
    {
        if (CurrentTask is null || IsRunning) return Task.CompletedTask;
        IsRunning = true;
        _lastTick = clock.Now;
        _session = new StudySession { TaskId = CurrentTask.Id, StartedAt = _lastTick, EndedAt = _lastTick };
        if (CurrentTask.Status == StudyTaskStatus.Pending) CurrentTask.Status = StudyTaskStatus.InProgress;
        OnStateChanged();
        return Task.CompletedTask;
    }

    public async Task PauseAsync(string reason = "pause")
    {
        if (!IsRunning) return;
        await TickAsync();
        IsRunning = false;
        if (_session is not null)
        {
            _session.EndReason = reason;
            await repository.SaveSessionAsync(_session);
            _session = null;
        }
        if (CurrentTask is not null) await repository.SaveTaskAsync(CurrentTask);
        OnStateChanged();
    }

    public async Task TickAsync()
    {
        if (!IsRunning || CurrentTask is null || _session is null) return;
        var now = clock.Now;
        if (now <= _lastTick) return;

        var cursor = _lastTick;
        while (cursor.Date < now.Date)
        {
            var boundary = new DateTimeOffset(cursor.Date.AddDays(1), cursor.Offset);
            ApplySegment(boundary - cursor, boundary, "midnight");
            await repository.SaveSessionAsync(_session);
            _session = new StudySession { TaskId = CurrentTask.Id, StartedAt = boundary, EndedAt = boundary };
            cursor = boundary;
        }

        ApplySegment(now - cursor, now, "heartbeat");
        _lastTick = now;
        await repository.SaveTaskAsync(CurrentTask);
        await repository.SaveSessionAsync(_session);
        OnStateChanged();
    }

    public async Task MoveAsync(int offset)
    {
        if (_tasks.Count == 0) return;
        await PauseAsync("switch-task");
        _currentIndex = Math.Clamp(_currentIndex + Math.Sign(offset), 0, _tasks.Count - 1);
        OnStateChanged();
    }

    public async Task AdjustAsync(int direction)
    {
        if (CurrentTask is null) return;
        TaskRules.Adjust(CurrentTask, direction);
        await repository.SaveTaskAsync(CurrentTask);
        OnStateChanged();
    }

    public async Task CompleteAsync()
    {
        if (CurrentTask is null) return;
        CurrentTask.CurrentValue = Math.Max(CurrentTask.CurrentValue, CurrentTask.TargetValue);
        if (CurrentTask.ProgressKind == ProgressKind.Time)
            CurrentTask.ElapsedSeconds = Math.Max(CurrentTask.ElapsedSeconds, (long)Math.Ceiling(CurrentTask.TargetValue * 60));
        CurrentTask.Status = StudyTaskStatus.Completed;
        await repository.SaveTaskAsync(CurrentTask);
        OnStateChanged();
    }

    private void ApplySegment(TimeSpan duration, DateTimeOffset endedAt, string reason)
    {
        if (CurrentTask is null || _session is null || duration <= TimeSpan.Zero) return;
        TaskRules.AddElapsed(CurrentTask, duration);
        _session.EndedAt = endedAt;
        _session.DurationSeconds += (long)Math.Floor(duration.TotalSeconds);
        _session.EndReason = reason;
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
