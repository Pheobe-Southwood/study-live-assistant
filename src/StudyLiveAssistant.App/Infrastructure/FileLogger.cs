namespace StudyLiveAssistant.App.Infrastructure;

public sealed class FileLogger(string logDirectory)
{
    public void Error(Exception exception, string context)
    {
        try
        {
            Directory.CreateDirectory(logDirectory);
            var path = Path.Combine(logDirectory, $"{DateTime.Today:yyyy-MM-dd}.log");
            File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] {context}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
