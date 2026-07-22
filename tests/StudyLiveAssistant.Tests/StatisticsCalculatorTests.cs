using StudyLiveAssistant.Core;

namespace StudyLiveAssistant.Tests;

public sealed class StatisticsCalculatorTests
{
    [Fact]
    public void Calculate_FiltersDateRangeAndCountsCompletedTasks()
    {
        var sessions = new[]
        {
            Session(new DateTimeOffset(2026, 7, 22, 8, 0, 0, TimeSpan.Zero), 3600),
            Session(new DateTimeOffset(2026, 7, 21, 8, 0, 0, TimeSpan.Zero), 1800),
            Session(new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero), 9999)
        };
        var tasks = new[]
        {
            new StudyTask { Date = new DateOnly(2026, 7, 22), Status = StudyTaskStatus.Completed },
            new StudyTask { Date = new DateOnly(2026, 7, 21), Status = StudyTaskStatus.InProgress },
            new StudyTask { Date = new DateOnly(2026, 7, 20), Status = StudyTaskStatus.Completed }
        };

        var result = StatisticsCalculator.Calculate(sessions, tasks, new DateOnly(2026, 7, 21), new DateOnly(2026, 7, 22));

        Assert.Equal(TimeSpan.FromMinutes(90), result.Duration);
        Assert.Equal(1, result.CompletedTasks);
    }

    [Fact]
    public void DefaultHotkeys_AreUnique()
    {
        var bindings = DefaultHotkeys.Create();
        Assert.Equal(bindings.Count, bindings.Select(item => (item.Modifiers, item.VirtualKey)).Distinct().Count());
    }

    private static StudySession Session(DateTimeOffset start, long seconds) => new()
    {
        TaskId = Guid.NewGuid(), StartedAt = start, EndedAt = start.AddSeconds(seconds), DurationSeconds = seconds
    };
}
