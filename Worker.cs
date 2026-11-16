
using System.Text.Json;

namespace LabMonitorAgent;

public class Worker(
    ILogger<Worker> logger,
    AgentLogWriter logWriter,
    ComputerInfo computerInfo,
    NetworkInfo networkInfo,
    ComputerIdentity computerIdentity
) : BackgroundService
{
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

        string json = JsonSerializer.Serialize(
            computer,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true
            }
        );

        logger.LogInformation("Computer Information JSON:");
        logger.LogInformation("{json}", json);

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
}
