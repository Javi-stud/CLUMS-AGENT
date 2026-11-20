
using System.Net.Http;

namespace LabMonitorAgent;

public class BackendMonitor(
    IHttpClientFactory httpClientFactory,
    ILogger<BackendMonitor> logger
)
{
    public async Task<bool> IsAvailableAsync(
        CancellationToken cancellationToken
    )
    {
        HttpClient client = httpClientFactory.CreateClient("CLUMS");

        try
        {
            using HttpResponseMessage response = await client.GetAsync(
                "api/health",
                cancellationToken
            );

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                "Backend is unavailable: {message}",
                ex.Message
            );

            return false;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Backend health check timed out.");
            return false;
        }
    }
}
