namespace StudyLiveAssistant.Core;

public interface IClock
{
    DateTimeOffset Now { get; }
    DateOnly Today { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
}

public interface ITaskRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task SaveCategoryAsync(TaskCategory category, CancellationToken cancellationToken = default);
    Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudyTask>> GetTasksAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task SaveTaskAsync(StudyTask task, CancellationToken cancellationToken = default);
    Task DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default);
    Task CopyTasksAsync(DateOnly source, DateOnly destination, CancellationToken cancellationToken = default);
    Task<DailyPlan?> GetDailyPlanAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task SaveDailyPlanAsync(DailyPlan plan, CancellationToken cancellationToken = default);
    Task SaveSessionAsync(StudySession session, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudySession>> GetSessionsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

public interface ISettingsRepository
{
    Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CountdownEvent>> GetCountdownEventsAsync(CancellationToken cancellationToken = default);
    Task SaveCountdownEventAsync(CountdownEvent countdown, CancellationToken cancellationToken = default);
    Task DeleteCountdownEventAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IStudySessionService
{
    IReadOnlyList<StudyTask> Tasks { get; }
    StudyTask? CurrentTask { get; }
    StudyTask? NextTask { get; }
    bool IsRunning { get; }
    event EventHandler? StateChanged;
    Task LoadDateAsync(DateOnly date, Guid? preferredTaskId = null);
    Task StartAsync();
    Task PauseAsync(string reason = "pause");
    Task TickAsync();
    Task MoveAsync(int offset);
    Task AdjustAsync(int direction);
    Task CompleteAsync();
}

public interface IHotkeyService : IDisposable
{
    event EventHandler<HotkeyAction>? Triggered;
    IReadOnlyList<string> Register(IEnumerable<HotkeyBinding> bindings);
    void UnregisterAll();
}
