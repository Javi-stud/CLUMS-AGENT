using System.Net.Http.Json;

namespace LabMonitorAgent;

public class AgentEventSender(
    IHttpClientFactory httpClientFactory,
    AgentEventQueue eventQueue,
    ILogger<AgentEventSender> logger
)
{
    public async Task<bool> SendPendingEventsAsync(
        Guid computerUuid,
        CancellationToken cancellationToken
    )
    {
        HttpClient client = httpClientFactory.CreateClient("CLUMS");

        List<AgentEvent> batch = eventQueue.GetPendingBatch(100);

        if (batch.Count == 0)
        {
            return true;
        }

        var payload = new
        {
            ComputerUuid = computerUuid,
            Events = batch
        };

        try
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync(
                "api/agent/events",
                payload,
                AgentJson.Options,
                cancellationToken
            );

            response.EnsureSuccessStatusCode();

            eventQueue.Acknowledge(
                batch.Select(e => e.EventId).ToList()
            );

            logger.LogInformation(
                "Successfully sent and acknowledged {count} events.",
                batch.Count
            );

            return true;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to send events. They remain stored for retry."
            );

            return false;
        }
    }
}