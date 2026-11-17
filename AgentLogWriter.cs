namespace LabMonitorAgent;

public class AgentLogWriter
{
    private readonly string logDirectory;
    private readonly string logFilePath;

    public AgentLogWriter()
    {
        // ProgramData, so the log is in the same place no matter which account runs the agent.
        logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
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
        try
        {
            Directory.CreateDirectory(logDirectory);

            string timestamp = DateTimeOffset.Now.ToString(
                "yyyy-MM-dd HH:mm:ss zzz"
            );

            string logEntry = $"[{timestamp}] {eventName}{Environment.NewLine}";

            await File.AppendAllTextAsync(logFilePath, logEntry);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A full disk or locked file must never stop the agent from monitoring.
        }
    }
}