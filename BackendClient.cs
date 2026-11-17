using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LabMonitorAgent;

public enum ApiStatus
{
    Ok,
    NotRegistered, // 404: the server doesn't know this computer yet -> register again
    Unauthorized,  // 401/403: wrong AgentApiKey. Keep queued events; an admin must fix the key
    Rejected,      // 4xx: the server refuses this particular request (bad data)
    Unreachable    // network down, timeout, 5xx, 429: try again later
}

public record ApiResult<T>(ApiStatus Status, T? Value = default, string? Detail = null);

/// <summary>Reply to register and heartbeat: the settings to use, plus any waiting commands.</summary>
public class AgentConfig
{
    public int ComputerId { get; set; }
    public int HeartbeatIntervalSeconds { get; set; } = 10;
    public int DisconnectionTimeoutSeconds { get; set; } = 30;
    public string LatestAgentVersion { get; set; } = "";
    public List<AgentCommand> Commands { get; set; } = new();
}

public class AgentCommand
{
    public int Id { get; set; }
    public string Type { get; set; } = ""; // "ping" | "update"
}

public class EventBatchResult
{
    public int Accepted { get; set; }
    public int Duplicates { get; set; }
}

/// <summary>The three calls the agent makes to the CLUMS server. Never throws for network problems.</summary>
public class BackendClient(HttpClient http, ILogger<BackendClient> logger)
{
    private class HeartbeatRequest
    {
        public string ComputerUuid { get; set; } = "";
        public string AgentVersion { get; set; } = "";
    }

    private class EventBatchRequest
    {
        public string ComputerUuid { get; set; } = "";
        public List<AgentEvent> Events { get; set; } = new();
    }

    public Task<ApiResult<AgentConfig>> RegisterAsync(ComputerDetails computer, CancellationToken ct) =>
        PostAsync<AgentConfig>("api/agent/register", computer, ct);

    public Task<ApiResult<AgentConfig>> HeartbeatAsync(string computerUuid, string agentVersion, CancellationToken ct) =>
        PostAsync<AgentConfig>(
            "api/agent/heartbeat",
            new HeartbeatRequest { ComputerUuid = computerUuid, AgentVersion = agentVersion },
            ct);

    public Task<ApiResult<EventBatchResult>> SendEventsAsync(string computerUuid, List<AgentEvent> events, CancellationToken ct) =>
        PostAsync<EventBatchResult>(
            "api/agent/events",
            new EventBatchRequest { ComputerUuid = computerUuid, Events = events },
            ct);

    private async Task<ApiResult<T>> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await http.PostAsJsonAsync(path, body, ApiJson.Options, ct);

            if (response.IsSuccessStatusCode)
            {
                T? value = await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, ct);
                return value is null
                    ? new ApiResult<T>(ApiStatus.Unreachable, Detail: "empty response")
                    : new ApiResult<T>(ApiStatus.Ok, value);
            }

            string detail = await ReadDetailAsync(response, ct);
            ApiStatus status = response.StatusCode switch
            {
                HttpStatusCode.NotFound => ApiStatus.NotRegistered,
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ApiStatus.Unauthorized,
                HttpStatusCode.TooManyRequests => ApiStatus.Unreachable,
                >= HttpStatusCode.InternalServerError => ApiStatus.Unreachable,
                _ => ApiStatus.Rejected
            };
            return new ApiResult<T>(status, Detail: $"HTTP {(int)response.StatusCode}: {detail}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // the agent is shutting down
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogDebug(ex, "Request to {path} failed", path);
            return new ApiResult<T>(ApiStatus.Unreachable, Detail: ex.Message);
        }
    }

    private static async Task<string> ReadDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            string text = await response.Content.ReadAsStringAsync(ct);
            return text.Length > 300 ? text[..300] : text;
        }
        catch (Exception)
        {
            return "";
        }
    }
}