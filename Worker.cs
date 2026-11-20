
using System.Text.Json;
using System.Net.Http.Json;

namespace LabMonitorAgent;

public class Worker(
    ILogger<Worker> logger,
    AgentLogWriter logWriter,
    ComputerInfo computerInfo,
    NetworkInfo networkInfo,
    ComputerIdentity computerIdentity,
    IHttpClientFactory httpClientFactory,
    BackendMonitor backendMonitor,
    AgentEventSender eventSender
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await logWriter.LogEventAsync("AGENT_STARTED");
        logger.LogInformation("Agent Started");

        HttpClient client = httpClientFactory.CreateClient("CLUMS");

        ComputerDetails computer = new ComputerDetails
        {
            ComputerUuid = computerIdentity.GetOrCreateUuid(),
            ComputerName = computerInfo.GetComputerName(),
            IpAddress = networkInfo.GetEthernetIPAddress(),
            MacAddress = networkInfo.GetEthernetMacAddress(),
            AgentVersion = typeof(Worker).Assembly.GetName().Version?.ToString() ?? "1.0.0"
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

        string? activeUsername = computerInfo.GetActiveUsername();

if (string.IsNullOrWhiteSpace(activeUsername))
{
    logger.LogInformation("No active Windows user detected.");
}
else
{
    logger.LogInformation(
        "Active Windows user: {username}",
        activeUsername
    );
}

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // STEP 1: Check backend availability
                bool isAvailable = await backendMonitor.IsAvailableAsync(
                    stoppingToken
                );

                if (!isAvailable)
                {
                    logger.LogWarning(
                        "Backend unavailable. Retrying in 30 seconds."
                    );

                    await Task.Delay(
                        TimeSpan.FromSeconds(30),
                        stoppingToken
                    );

                    continue;
                }

                logger.LogInformation("Backend is available!");

                // STEP 2: Register computer
                AgentConfig? config;

                // STEP 1: Send heartbeat
try
{
    using HttpResponseMessage response = await client.PostAsJsonAsync(
        "api/agent/heartbeat",
        heartbeat,
        AgentJson.Options,
        stoppingToken
    );

    response.EnsureSuccessStatusCode();

    logger.LogInformation("Heartbeat sent successfully.");
}
catch (OperationCanceledException)
    when (stoppingToken.IsCancellationRequested)
{
    break;
}
catch (Exception ex)
{
    logger.LogError(ex, "Heartbeat failed. Reconnecting...");
    connectionLost = true;
    break;
}

// STEP 2: Send pending events
try
{
    bool eventsSent = await eventSender.SendPendingEventsAsync(
        Guid.Parse(computer.ComputerUuid),
        stoppingToken
    );

    if (!eventsSent)
    {
        logger.LogWarning(
            "Some events remain queued for the next retry."
        );
    }
}
catch (OperationCanceledException)
    when (stoppingToken.IsCancellationRequested)
{
    break;
}
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Computer registration failed.");

                    await Task.Delay(
                        TimeSpan.FromSeconds(30),
                        stoppingToken
                    );

                    continue;
                }

                // STEP 3: Send heartbeats
                bool connectionLost = false;

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(config.HeartbeatIntervalSeconds),
                        stoppingToken
                    );

                    var heartbeat = new
                    {
                        ComputerUuid = computer.ComputerUuid,
                        AgentVersion = computer.AgentVersion
                    };

                    try
                    {
                        using HttpResponseMessage response = await client.PostAsJsonAsync(
                            "api/agent/heartbeat",
                            heartbeat,
                            new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                            },
                            stoppingToken
                        );

                        response.EnsureSuccessStatusCode();

                        logger.LogInformation("Heartbeat sent successfully.");
                        bool eventsSent = await eventSender.SendPendingEventsAsync(
    Guid.Parse(computer.ComputerUuid),
    stoppingToken
);

if (!eventsSent)
{
    logger.LogWarning("Some events remain queued for retry.");
}
                    }
                    catch (OperationCanceledException)
                        when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Heartbeat failed. Reconnecting...");
                        connectionLost = true;
                        break;
                    }
                }

                if (stoppingToken.IsCancellationRequested)
                    break;

                if (connectionLost)
                {
                    logger.LogWarning(
                        "Connection lost. Returning to backend availability checks."
                    );
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Expected when the agent is shutting down.
        }
        finally
        {
            await logWriter.LogEventAsync("AGENT_STOPPED");
            logger.LogInformation("Agent Stopped");
        }
    }
}
