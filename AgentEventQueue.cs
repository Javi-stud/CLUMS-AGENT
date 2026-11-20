using System.Text.Json;

namespace LabMonitorAgent;

public class AgentEventQueue
{
    private readonly List<AgentEvent> events = [];
    private readonly object queueLock = new();

    private readonly string filePath;

    public AgentEventQueue()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData
            ),
            "CLUMS",
            "Agent"
        );

        Directory.CreateDirectory(directory);

        filePath = Path.Combine(
            directory,
            "pending-events.json"
        );

        LoadEvents();
    }

    public void Enqueue(AgentEvent agentEvent)
    {
        lock (queueLock)
        {
            events.Add(agentEvent);
            SaveEvents();
        }
    }

    public List<AgentEvent> GetPendingBatch(int maxCount)
    {
        lock (queueLock)
        {
            return events.Take(maxCount).ToList();
        }
    }

    public void Acknowledge(List<Guid> eventIds)
    {
        lock (queueLock)
        {
            HashSet<Guid> ids = eventIds.ToHashSet();

            events.RemoveAll(e => ids.Contains(e.EventId));

            SaveEvents();
        }
    }

    public int Count
    {
        get
        {
            lock (queueLock)
            {
                return events.Count;
            }
        }
    }

    private void LoadEvents()
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        string json = File.ReadAllText(filePath);

        List<AgentEvent>? savedEvents =
            JsonSerializer.Deserialize<List<AgentEvent>>(
                json,
                AgentJson.Options
            );

        if (savedEvents != null)
        {
            events.AddRange(savedEvents);
        }
    }

    private void SaveEvents()
    {
        string json = JsonSerializer.Serialize(
            events,
            AgentJson.Options
        );

        string temporaryFile = filePath + ".tmp";

        try
        {
            File.WriteAllText(temporaryFile, json);

            File.Move(
                temporaryFile,
                filePath,
                overwrite: true
            );
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
    }
}