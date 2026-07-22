using StudyLiveAssistant.Core;

namespace StudyLiveAssistant.Tests;

public sealed class TaskRulesTests
{
    [Fact]
    public void Sort_UsesStartTimeThenCreationOrder()
    {
        var category = Guid.NewGuid();
        var tasks = new[]
        {
            CreateTask(category, new TimeOnly(9, 0), 2),
            CreateTask(category, new TimeOnly(8, 0), 3),
            CreateTask(category, new TimeOnly(9, 0), 1)
        };

        var sorted = TaskRules.Sort(tasks);

        Assert.Equal(new[] { 3L, 1L, 2L }, sorted.Select(task => task.CreationOrder));
    }

    [Fact]
    public void Adjust_TimeTaskChangesElapsedMinutesAndCompletion()
    {
        var task = ValidTask(ProgressKind.Time, ProgressUnit.Minute);
        task.TargetValue = 2;

        TaskRules.Adjust(task, 1);
        TaskRules.Adjust(task, 1);

        Assert.Equal(120, task.ElapsedSeconds);
        Assert.Equal(2, task.CurrentValue);
        Assert.Equal(StudyTaskStatus.Completed, task.Status);

        TaskRules.Adjust(task, -1);
        Assert.Equal(StudyTaskStatus.InProgress, task.Status);
    }

    [Fact]
    public void Adjust_CountTaskKeepsElapsedSeparate()
    {
        var task = ValidTask(ProgressKind.Count, ProgressUnit.Page);
        task.TargetValue = 10;
        task.AdjustmentStep = 2;
        task.ElapsedSeconds = 45;

        TaskRules.Adjust(task, 1);

        Assert.Equal(2, task.CurrentValue);
        Assert.Equal(45, task.ElapsedSeconds);
    }

    [Theory]
    [InlineData(ProgressKind.Time, ProgressUnit.Page)]
    [InlineData(ProgressKind.Count, ProgressUnit.Minute)]
    public void Validate_RejectsMismatchedKindAndUnit(ProgressKind kind, ProgressUnit unit)
    {
        var task = ValidTask(kind, unit);
        Assert.Throws<ArgumentException>(() => TaskRules.Validate(task));
    }

    private static StudyTask ValidTask(ProgressKind kind, ProgressUnit unit)
    {
        var category = Guid.NewGuid();
        return CreateTask(category, new TimeOnly(8, 0), 1, kind, unit);
    }

    private static StudyTask CreateTask(Guid category, TimeOnly start, long order, ProgressKind kind = ProgressKind.Time, ProgressUnit unit = ProgressUnit.Minute) => new()
    {
        PrimaryCategoryId = category, SecondaryCategoryId = Guid.NewGuid(), ScheduledStart = start,
        CreationOrder = order, ProgressKind = kind, Unit = unit, TargetValue = 60, AdjustmentStep = 1
    };
}
