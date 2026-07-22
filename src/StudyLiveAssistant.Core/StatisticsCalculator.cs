namespace StudyLiveAssistant.Core;

public static class StatisticsCalculator
{
    public static StudyStatistics Calculate(
        IEnumerable<StudySession> sessions,
        IEnumerable<StudyTask> tasks,
        DateOnly from,
        DateOnly to)
    {
        var duration = sessions
            .Where(s => DateOnly.FromDateTime(s.StartedAt.LocalDateTime) >= from && DateOnly.FromDateTime(s.StartedAt.LocalDateTime) <= to)
            .Sum(s => Math.Max(0, s.DurationSeconds));
        var completed = tasks.Count(t => t.Date >= from && t.Date <= to && t.Status == StudyTaskStatus.Completed);
        return new StudyStatistics(TimeSpan.FromSeconds(duration), completed);
    }
}
