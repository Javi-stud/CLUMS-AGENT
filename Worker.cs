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

    ComputerDetails computer = new ComputerDetails
{
    ComputerUuid = computerIdentity.GetOrCreateUuid(),
    ComputerName = computerInfo.GetComputerName(),
    IpAddress = networkInfo.GetEthernetIPAddress(),
    MacAddress = networkInfo.GetEthernetMacAddress()
};

    logger.LogInformation("Computer UUID: {uuid}", computer.ComputerUuid);
    logger.LogInformation("Computer Name: {name}", computer.ComputerName);
    logger.LogInformation("IP Address: {ip}", computer.IpAddress);
    logger.LogInformation("MAC Address: {mac}", computer.MacAddress);

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