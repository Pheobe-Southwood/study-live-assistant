using StudyLiveAssistant.Core;

namespace StudyLiveAssistant.Tests;

public sealed class StudySessionServiceTests
{
    [Fact]
    public async Task Tick_AccumulatesTimeForCountTaskWithoutChangingCountProgress()
    {
        var repository = new MemoryRepository();
        var task = repository.AddTask(ProgressKind.Count, ProgressUnit.Question, new TimeOnly(8, 0));
        var clock = new FakeClock(new DateTimeOffset(2026, 7, 22, 8, 0, 0, TimeSpan.Zero));
        var service = new StudySessionService(repository, clock);
        await service.LoadDateAsync(task.Date);

        await service.StartAsync();
        clock.Advance(TimeSpan.FromSeconds(95));
        await service.TickAsync();

        Assert.Equal(95, task.ElapsedSeconds);
        Assert.Equal(0, task.CurrentValue);
        Assert.True(service.IsRunning);
    }

    [Fact]
    public async Task Move_PausesCurrentAndSelectsAdjacentTask()
    {
        var repository = new MemoryRepository();
        var first = repository.AddTask(ProgressKind.Time, ProgressUnit.Minute, new TimeOnly(8, 0));
        var second = repository.AddTask(ProgressKind.Time, ProgressUnit.Minute, new TimeOnly(9, 0));
        var clock = new FakeClock(new DateTimeOffset(2026, 7, 22, 8, 0, 0, TimeSpan.Zero));
        var service = new StudySessionService(repository, clock);
        await service.LoadDateAsync(first.Date);
        await service.StartAsync();

        await service.MoveAsync(1);

        Assert.False(service.IsRunning);
        Assert.Equal(second.Id, service.CurrentTask?.Id);
        Assert.Single(repository.Sessions);
    }

    [Fact]
    public async Task Start_CapturesActualStartOnlyOnFirstRun()
    {
        var repository = new MemoryRepository();
        var task = repository.AddTask(ProgressKind.Count, ProgressUnit.Chapter, new TimeOnly(10, 0));
        var firstStart = new DateTimeOffset(2026, 7, 22, 8, 12, 30, TimeSpan.Zero);
        var clock = new FakeClock(firstStart);
        var service = new StudySessionService(repository, clock);
        await service.LoadDateAsync(task.Date);

        await service.StartAsync();
        await service.PauseAsync();
        clock.Advance(TimeSpan.FromHours(1));
        await service.StartAsync();

        Assert.Equal(firstStart, task.ActualStartedAt);
        Assert.Equal(new TimeOnly(10, 0), task.ScheduledStart);
    }

    [Fact]
    public async Task Tick_SplitsSessionAtMidnight()
    {
        var repository = new MemoryRepository();
        var task = repository.AddTask(ProgressKind.Time, ProgressUnit.Minute, new TimeOnly(23, 0));
        var clock = new FakeClock(new DateTimeOffset(2026, 7, 22, 23, 59, 30, TimeSpan.Zero));
        var service = new StudySessionService(repository, clock);
        await service.LoadDateAsync(task.Date);
        await service.StartAsync();

        clock.Advance(TimeSpan.FromMinutes(1));
        await service.TickAsync();

        Assert.Equal(60, task.ElapsedSeconds);
        Assert.Equal(2, repository.Sessions.Count);
        Assert.Equal(60, repository.Sessions.Sum(session => session.DurationSeconds));
        Assert.Equal(30, repository.Sessions.Last().DurationSeconds);
    }

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now { get; private set; } = now;
        public DateOnly Today => DateOnly.FromDateTime(Now.LocalDateTime);
        public void Advance(TimeSpan duration) => Now += duration;
    }

    private sealed class MemoryRepository : ITaskRepository
    {
        private readonly List<StudyTask> _tasks = [];
        private readonly Dictionary<Guid, StudySession> _sessions = [];
        public IReadOnlyCollection<StudySession> Sessions => _sessions.Values;

        public StudyTask AddTask(ProgressKind kind, ProgressUnit unit, TimeOnly start)
        {
            var task = new StudyTask
            {
                Date = new DateOnly(2026, 7, 22), PrimaryCategoryId = Guid.NewGuid(), SecondaryCategoryId = Guid.NewGuid(),
                ProgressKind = kind, Unit = unit, ScheduledStart = start, TargetValue = 60, AdjustmentStep = 1,
                CreationOrder = _tasks.Count
            };
            _tasks.Add(task);
            return task;
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TaskCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TaskCategory>>([]);
        public Task SaveCategoryAsync(TaskCategory category, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<StudyTask>> GetTasksAsync(DateOnly date, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StudyTask>>(_tasks.Where(task => task.Date == date).ToList());
        public Task SaveTaskAsync(StudyTask task, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CopyTasksAsync(DateOnly source, DateOnly destination, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<DailyPlan?> GetDailyPlanAsync(DateOnly date, CancellationToken cancellationToken = default) => Task.FromResult<DailyPlan?>(null);
        public Task SaveDailyPlanAsync(DailyPlan plan, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveSessionAsync(StudySession session, CancellationToken cancellationToken = default)
        {
            _sessions[session.Id] = new StudySession
            {
                Id = session.Id, TaskId = session.TaskId, StartedAt = session.StartedAt, EndedAt = session.EndedAt,
                DurationSeconds = session.DurationSeconds, EndReason = session.EndReason
            };
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<StudySession>> GetSessionsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StudySession>>(_sessions.Values.ToList());
    }
}
