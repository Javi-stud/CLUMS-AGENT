using System.Text.Json;
using Microsoft.Extensions.Options;

namespace LabMonitorAgent;

public class Worker(
    ILogger<Worker> logger,
    IOptions<BackendOptions> backendOptions,
    AgentLogWriter logWriter,
    ComputerInfo computerInfo,
    NetworkInfo networkInfo,
    ComputerIdentity computerIdentity,
    BackendClient backend,
    EventQueue queue,
    ApplicationMonitor applicationMonitor,
    WindowsSessionMonitor windowsSessionMonitor
) : BackgroundService
{
    private const int BatchSize = 100;

    private ComputerDetails computer = new();
    private bool registered;
    private int intervalSeconds = 10;           // replaced by the value the server sends
    private bool connected = true;              // for "connection lost / restored" messages
    private string lastFailureKey = "";
    private DateTimeOffset lastFailureLoggedAt = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await logWriter.LogEventAsync(AgentEventTypes.AgentStarted);
        await queue.EnqueueAsync(AgentEvent.Create(AgentEventTypes.AgentStarted));
        logger.LogInformation("Agent started (v{version})", AgentInfo.Version);
        WarnIfKeyTravelsUnencrypted();

        computer = BuildComputerDetails();

Task sessionMonitoringTask =
    windowsSessionMonitor.RunAsync(stoppingToken);

logger.LogInformation("Windows session monitoring task started");

// Start application monitoring after the student login event.
Task applicationMonitoringTask =
    applicationMonitor.RunAsync(stoppingToken);

logger.LogInformation("Application monitoring task started");

logger.LogInformation("Computer Information JSON:");
        logger.LogInformation("{json}", JsonSerializer.Serialize(computer, ApiJson.Indented));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TickAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Whatever went wrong, keep monitoring.
                    logger.LogError(ex, "Unexpected error; will retry");
                }

                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // normal shutdown, handled below
        }
        
        await Task.WhenAll(
    applicationMonitoringTask,
    sessionMonitoringTask
);
        await logWriter.LogEventAsync(AgentEventTypes.AgentStopped);
        await queue.EnqueueAsync(AgentEvent.Create(AgentEventTypes.AgentStopped));
        await TryFinalFlushAsync();
        logger.LogInformation("Agent stopped");
    }

    /// <summary>One round: register or heartbeat, then send any queued events.</summary>
    private async Task TickAsync(CancellationToken ct)
    {
        // The IP or MAC can change (cable moved, new DHCP lease): tell the server again.
        ComputerDetails current = BuildComputerDetails();
        if (registered && (current.IpAddress != computer.IpAddress || current.MacAddress != computer.MacAddress))
        {
            logger.LogInformation("Network details changed; registering again");
            registered = false;
        }
        computer = current;

        if (!registered)
        {
            ApiResult<AgentConfig> reg = await backend.RegisterAsync(computer, ct);
            if (reg.Status != ApiStatus.Ok)
            {
                ReportFailure("register", reg.Status, reg.Detail);
                return;
            }

            registered = true;
            MarkConnected();
            logger.LogInformation("Registered with the server as computer #{id}", reg.Value!.ComputerId);
            ApplyConfig(reg.Value);
        }
        else
        {
            ApiResult<AgentConfig> hb = await backend.HeartbeatAsync(computer.ComputerUuid, AgentInfo.Version, ct);
            switch (hb.Status)
            {
                case ApiStatus.Ok:
                    MarkConnected();
                    ApplyConfig(hb.Value!);
                    break;
                case ApiStatus.NotRegistered:
                    logger.LogWarning("The server doesn't know this computer; registering again");
                    registered = false;
                    return;
                default:
                    ReportFailure("heartbeat", hb.Status, hb.Detail);
                    return;
            }
        }

        await FlushQueueAsync(ct);
    }

    // ---- sending queued events ----

    private async Task FlushQueueAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            List<AgentEvent> batch = await queue.PeekAsync(BatchSize);
            if (batch.Count == 0) return;

            if (!await SendBatchAsync(batch, ct)) return;
        }
    }

    /// <summary>Returns true if the batch is finished with (sent or dropped) and sending can continue.</summary>
    private async Task<bool> SendBatchAsync(List<AgentEvent> batch, CancellationToken ct)
    {
        ApiResult<EventBatchResult> result = await backend.SendEventsAsync(computer.ComputerUuid, batch, ct);

        switch (result.Status)
        {
            case ApiStatus.Ok:
                await queue.RemoveAsync(batch);
                logger.LogInformation(
                    "Sent {count} event(s) ({accepted} new, {dup} already known)",
                    batch.Count, result.Value!.Accepted, result.Value.Duplicates);
                return true;

            case ApiStatus.Rejected when batch.Count > 1:
                // One bad event shouldn't block the rest: try them one at a time.
                foreach (AgentEvent ev in batch)
                {
                    if (!await SendBatchAsync(new List<AgentEvent> { ev }, ct)) return false;
                }
                return true;

            case ApiStatus.Rejected:
                logger.LogError(
                    "The server rejected event {type} ({id}): {detail}. Dropping it.",
                    batch[0].Type, batch[0].EventId, result.Detail);
                await queue.RemoveAsync(batch);
                return true;

            case ApiStatus.NotRegistered:
                registered = false;
                return false;

            default:
                ReportFailure("send events", result.Status, result.Detail);
                return false;
        }
    }

    /// <summary>On shutdown, give the server a few seconds to receive AGENT_STOPPED. Otherwise it stays queued.</summary>
    private async Task TryFinalFlushAsync()
    {
        if (!registered) return;

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(3));
        try
        {
            await FlushQueueAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // out of time; the event is still in the queue for the next start
        }
    }

    // ---- server replies ----

    private void ApplyConfig(AgentConfig config)
    {
        int seconds = Math.Clamp(config.HeartbeatIntervalSeconds, 1, 300);
        if (seconds != intervalSeconds)
        {
            logger.LogInformation("Heartbeat interval is now {seconds} s (set by the server)", seconds);
            intervalSeconds = seconds;
        }

        foreach (AgentCommand command in config.Commands)
        {
            switch (command.Type)
            {
                case "ping":
                    logger.LogInformation("Ping received from the server");
                    break;
                case "update":
                    logger.LogWarning(
                        "The server asked this agent to update to v{latest}. Self-update isn't implemented yet.",
                        config.LatestAgentVersion);
                    break;
                default:
                    logger.LogWarning("Ignoring unknown command '{type}'", command.Type);
                    break;
            }
        }
    }

    // ---- helpers ----

    private ComputerDetails BuildComputerDetails() => new()
    {
        ComputerUuid = computerIdentity.GetOrCreateUuid(),
        ComputerName = computerInfo.GetComputerName(),
        IpAddress = networkInfo.GetIpAddress(),
        MacAddress = networkInfo.GetMacAddress(),
        AgentVersion = AgentInfo.Version
    };

    private void MarkConnected()
    {
        if (!connected)
        {
            connected = true;
            lastFailureKey = "";
            logger.LogInformation("Connection to the server restored");
        }
    }

    /// <summary>Log a failure, but not the same one every few seconds.</summary>
    private void ReportFailure(string what, ApiStatus status, string? detail)
    {
        if (status != ApiStatus.Rejected) connected = false;

        string key = $"{what}:{status}";
        if (key == lastFailureKey && DateTimeOffset.Now - lastFailureLoggedAt < TimeSpan.FromMinutes(1)) return;
        lastFailureKey = key;
        lastFailureLoggedAt = DateTimeOffset.Now;

        switch (status)
        {
            case ApiStatus.Unauthorized:
                logger.LogError(
                    "The server refused the agent's key while trying to {what}. " +
                    "Check Backend:AgentApiKey matches AGENT_API_KEY on the server. Events stay queued.",
                    what);
                break;
            case ApiStatus.Unreachable:
                logger.LogWarning(
                    "Can't reach the server to {what} ({detail}). Events are queued and will be sent when it is back.",
                    what, detail);
                break;
            default:
                logger.LogError("The server refused the request to {what}: {detail}", what, detail);
                break;
        }
    }

    private void WarnIfKeyTravelsUnencrypted()
    {
        if (Uri.TryCreate(backendOptions.Value.BaseUrl, UriKind.Absolute, out Uri? uri)
            && uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
        {
            logger.LogWarning(
                "Backend:BaseUrl uses http://, so the agent key crosses the network unencrypted. Use https:// if you can.");
        }
    }
}