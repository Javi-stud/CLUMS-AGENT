namespace LabMonitorAgent;

public class Worker(
    ILogger<Worker> logger,
    AgentLogWriter logWriter,
    ComputerInfo computerInfo,
    NetworkInfo networkInfo,
    ComputerIdentity computerIdentity
) : BackgroundService 
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
    await logWriter.LogEventAsync("AGENT_STARTED");
    logger.LogInformation("Agent Started");

    string computerName = computerInfo.GetComputerName();
    string ipAddress = networkInfo.GetEthernetIPAddress();
    string macAddress = networkInfo.GetEthernetMacAddress();
    string computerUuid = computerIdentity.GetOrCreateUuid();

    logger.LogInformation("Computer UUID: {uuid}", computerUuid);
    logger.LogInformation("MAC Address: {mac}", macAddress);
    logger.LogInformation("Computer Name: {name}", computerName);
    logger.LogInformation("IP Address: {ip}", ipAddress);

    try
    {
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
        await logWriter.LogEventAsync("AGENT_STOPPED");

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