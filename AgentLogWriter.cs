namespace LabMonitorAgent;

public class AgentLogWriter
{
    private readonly string logDirectory;
    private readonly string logFilePath;

    public AgentLogWriter()
    {
        logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabMonitorAgent",
            "Logs"
        );

        logFilePath = Path.Combine(
            logDirectory,
            "agent-log.txt"
        );
    }

    public async Task LogEventAsync(string eventName)
    {
        Directory.CreateDirectory(logDirectory);

        string timestamp = DateTimeOffset.Now.ToString(
            "yyyy-MM-dd HH:mm:ss zzz"
        );

        string logEntry = $"[{timestamp}] {eventName}{Environment.NewLine}";

        await File.AppendAllTextAsync(logFilePath, logEntry);
    }
}