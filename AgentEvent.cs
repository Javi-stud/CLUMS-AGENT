namespace LabMonitorAgent;

public class AgentEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();

    public string Type { get; set; } = "";

    public DateTime OccurredAt { get; set; }

    public string? ComputerUsername { get; set; }

    public string? AppName { get; set; }
}