using StudyLiveAssistant.App.Infrastructure;
using StudyLiveAssistant.Core;

namespace StudyLiveAssistant.Tests;

public sealed class LocalDatabaseTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "StudyLiveAssistantTests", Guid.NewGuid().ToString("N"));
    private LocalDatabase _database = null!;

    public async ValueTask InitializeAsync()
    {
        _database = new LocalDatabase(_directory);
        await _database.InitializeAsync();
    }

    public ValueTask DisposeAsync()
    {
        _database.Dispose();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Initialize_SeedsEditableCategoriesAndCountdown()
    {
        var categories = await _database.GetCategoriesAsync();
        var countdowns = await _database.GetCountdownEventsAsync();

        Assert.Contains(categories, category => category.Name == "数学" && category.ParentId is null);
        Assert.Contains(categories, category => category.Name == "真题卷" && category.ParentId is not null);
        Assert.Single(countdowns);
    }

    [Fact]
    public async Task TaskSettingsAndSession_RoundTrip()
    {
        var categories = await _database.GetCategoriesAsync();
        var primary = categories.First(category => category.ParentId is null);
        var secondary = categories.First(category => category.ParentId == primary.Id);
        var task = new StudyTask
        {
            Date = new DateOnly(2026, 7, 22), PrimaryCategoryId = primary.Id, SecondaryCategoryId = secondary.Id,
            Detail = "测试持久化", ScheduledStart = new TimeOnly(8, 30), ProgressKind = ProgressKind.Count,
            ActualStartedAt = new DateTimeOffset(2026, 7, 22, 8, 42, 0, TimeSpan.Zero),
            Unit = ProgressUnit.Page, TargetValue = 20, AdjustmentStep = 2
        };
        await _database.SaveTaskAsync(task);
        await _database.SaveSessionAsync(new StudySession
        {
            TaskId = task.Id, StartedAt = new DateTimeOffset(2026, 7, 22, 8, 30, 0, TimeSpan.Zero),
            EndedAt = new DateTimeOffset(2026, 7, 22, 9, 0, 0, TimeSpan.Zero), DurationSeconds = 1800, EndReason = "pause"
        });
        var settings = new AppSettings { CurrentTaskId = task.Id };
        settings.Appearance.Theme = CardTheme.Cute;
        await _database.SaveSettingsAsync(settings);

        var loadedTask = Assert.Single(await _database.GetTasksAsync(task.Date));
        var loadedSession = Assert.Single(await _database.GetSessionsAsync(task.Date, task.Date));
        var loadedSettings = await _database.LoadSettingsAsync();

        Assert.Equal("测试持久化", loadedTask.Detail);
        Assert.Equal(ProgressKind.Count, loadedTask.ProgressKind);
        Assert.Equal(ProgressUnit.Page, loadedTask.Unit);
        Assert.Equal(task.ActualStartedAt, loadedTask.ActualStartedAt);
        Assert.Equal(1800, loadedSession.DurationSeconds);
        Assert.Equal(CardTheme.Cute, loadedSettings.Appearance.Theme);
        Assert.Equal(task.Id, loadedSettings.CurrentTaskId);
    }

    [Fact]
    public async Task CopyTasks_CreatesFreshPendingTasks()
    {
        var categories = await _database.GetCategoriesAsync();
        var primary = categories.First(category => category.ParentId is null);
        var secondary = categories.First(category => category.ParentId == primary.Id);
        var source = new DateOnly(2026, 7, 21);
        var destination = source.AddDays(1);
        await _database.SaveTaskAsync(new StudyTask
        {
            Date = source, PrimaryCategoryId = primary.Id, SecondaryCategoryId = secondary.Id,
            ProgressKind = ProgressKind.Time, Unit = ProgressUnit.Minute, TargetValue = 60, CurrentValue = 60,
            ElapsedSeconds = 3600, Status = StudyTaskStatus.Completed
        });

        await _database.CopyTasksAsync(source, destination);

        var copy = Assert.Single(await _database.GetTasksAsync(destination));
        Assert.Equal(0, copy.CurrentValue);
        Assert.Equal(0, copy.ElapsedSeconds);
        Assert.Equal(StudyTaskStatus.Pending, copy.Status);
    }
}
