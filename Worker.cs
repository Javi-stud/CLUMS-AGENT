namespace LabMonitorAgent;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LabMonitorAgent",
        "Logs"
    );

    private static readonly string LogFilePath = Path.Combine(
        LogDirectory,
        "agent-log.txt"
    );

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LogEventAsync("AGENT_STARTED");

        logger.LogInformation("Agent Started");

        try
        {
            // Keep the worker running until shutdown is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Record that the agent stopped
            await LogEventAsync("AGENT_STOPPED");

            logger.LogInformation("Agent Stopped");
        }
    }

    private static async Task LogEventAsync(string eventName)
    {
        // Make sure the log folder exists
        Directory.CreateDirectory(LogDirectory);

        string timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");

        string logEntry = $"[{timestamp}] {eventName}{Environment.NewLine}";

        // Add the entry to the file
        await File.AppendAllTextAsync(LogFilePath, logEntry);
    }
}