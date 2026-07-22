namespace StudyLiveAssistant.Core;

public static class TaskRules
{
    public static IReadOnlyList<StudyTask> Sort(IEnumerable<StudyTask> tasks) => tasks
        .OrderBy(t => t.ScheduledStart)
        .ThenBy(t => t.CreationOrder)
        .ThenBy(t => t.Id)
        .ToList();

    public static void Validate(StudyTask task)
    {
        if (task.PrimaryCategoryId == Guid.Empty || task.SecondaryCategoryId == Guid.Empty)
            throw new ArgumentException("请选择一级和二级任务类型。");
        if (task.TargetValue <= 0) throw new ArgumentException("目标值必须大于零。");
        if (task.AdjustmentStep <= 0) throw new ArgumentException("调整步长必须大于零。");
        if (task.ProgressKind == ProgressKind.Time && task.Unit != ProgressUnit.Minute)
            throw new ArgumentException("时间型任务必须使用分钟作为进度单位。");
        if (task.ProgressKind == ProgressKind.Count && task.Unit == ProgressUnit.Minute)
            throw new ArgumentException("计数型任务不能使用分钟作为进度单位。");
        if (task.Unit == ProgressUnit.Custom && string.IsNullOrWhiteSpace(task.CustomUnit))
            throw new ArgumentException("自定义单位不能为空。");
    }

    public static void Adjust(StudyTask task, int direction)
    {
        var signedStep = Math.Sign(direction) * task.AdjustmentStep;
        if (task.ProgressKind == ProgressKind.Time)
        {
            task.ElapsedSeconds = Math.Max(0, task.ElapsedSeconds + (long)Math.Round(signedStep * 60));
            task.CurrentValue = task.ElapsedSeconds / 60d;
        }
        else
        {
            task.CurrentValue = Math.Max(0, task.CurrentValue + signedStep);
        }
        UpdateStatus(task);
    }

    public static void AddElapsed(StudyTask task, TimeSpan elapsed)
    {
        task.ElapsedSeconds = Math.Max(0, task.ElapsedSeconds + (long)Math.Floor(elapsed.TotalSeconds));
        if (task.ProgressKind == ProgressKind.Time) task.CurrentValue = task.ElapsedSeconds / 60d;
        if (task.Status == StudyTaskStatus.Pending) task.Status = StudyTaskStatus.InProgress;
        UpdateStatus(task);
    }

    public static void UpdateStatus(StudyTask task)
    {
        if (task.CurrentValue >= task.TargetValue) task.Status = StudyTaskStatus.Completed;
        else if (task.Status == StudyTaskStatus.Completed) task.Status = StudyTaskStatus.InProgress;
    }

    public static string UnitText(StudyTask task) => task.Unit switch
    {
        ProgressUnit.Minute => "分钟",
        ProgressUnit.Chapter => "章",
        ProgressUnit.Page => "页",
        ProgressUnit.Question => "题",
        _ => task.CustomUnit
    };
}
