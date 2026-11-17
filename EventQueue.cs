using System.Text.Json;

namespace LabMonitorAgent;

/// <summary>
/// Events waiting to be sent, kept in a file (one JSON object per line) so nothing is lost
/// if the server is down, the network drops, or the PC is switched off.
/// </summary>
public class EventQueue
{
    private const int MaxQueuedEvents = 10_000; // if the server is gone for weeks, drop the oldest

    private readonly string directory;
    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly ILogger<EventQueue> logger;

    public EventQueue(ILogger<EventQueue> logger)
    {
        this.logger = logger;
        directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "LabMonitorAgent");
        filePath = Path.Combine(directory, "event-queue.jsonl");
    }

    public async Task EnqueueAsync(AgentEvent ev)
    {
        await gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(directory);
            string line = JsonSerializer.Serialize(ev, ApiJson.Options) + Environment.NewLine;
            await File.AppendAllTextAsync(filePath, line);
            await TrimAsync();
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Oldest events first. Lines that can't be read (e.g. half-written at power loss) are skipped.</summary>
    public async Task<List<AgentEvent>> PeekAsync(int max)
    {
        await gate.WaitAsync();
        try
        {
            List<AgentEvent> result = new();
            foreach (AgentEvent ev in await ReadAllAsync())
            {
                result.Add(ev);
                if (result.Count >= max) break;
            }
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RemoveAsync(IEnumerable<AgentEvent> sent)
    {
        HashSet<Guid> ids = sent.Select(e => e.EventId).ToHashSet();
        if (ids.Count == 0) return;

        await gate.WaitAsync();
        try
        {
            List<AgentEvent> remaining = (await ReadAllAsync()).Where(e => !ids.Contains(e.EventId)).ToList();
            await WriteAllAsync(remaining);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<int> CountAsync()
    {
        await gate.WaitAsync();
        try
        {
            return (await ReadAllAsync()).Count;
        }
        finally
        {
            gate.Release();
        }
    }

    // ---- file helpers (callers hold the gate) ----

    private async Task<List<AgentEvent>> ReadAllAsync()
    {
        List<AgentEvent> events = new();
        if (!File.Exists(filePath)) return events;

        foreach (string line in await File.ReadAllLinesAsync(filePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                AgentEvent? ev = JsonSerializer.Deserialize<AgentEvent>(line, ApiJson.Options);
                if (ev is not null && ev.EventId != Guid.Empty && !string.IsNullOrEmpty(ev.Type))
                    events.Add(ev);
            }
            catch (JsonException)
            {
                logger.LogWarning("Skipping an unreadable line in the event queue");
            }
        }
        return events;
    }

    private async Task WriteAllAsync(List<AgentEvent> events)
    {
        Directory.CreateDirectory(directory);
        string temp = filePath + ".tmp";
        string text = string.Concat(events.Select(e => JsonSerializer.Serialize(e, ApiJson.Options) + Environment.NewLine));
        await File.WriteAllTextAsync(temp, text);
        File.Move(temp, filePath, overwrite: true); // swap in one step so a crash can't leave a half-written queue
    }

    private async Task TrimAsync()
    {
        List<AgentEvent> all = await ReadAllAsync();
        if (all.Count <= MaxQueuedEvents) return;

        int drop = all.Count - MaxQueuedEvents;
        logger.LogWarning("Event queue is full; dropping the {count} oldest events", drop);
        await WriteAllAsync(all.Skip(drop).ToList());
    }
}