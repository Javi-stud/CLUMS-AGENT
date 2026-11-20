namespace LabMonitorAgent;

public class AgentConfig
{
    public int HeartbeatIntervalSeconds { get; set; }

    public int DisconnectionTimeoutSeconds { get; set; }

    public string LatestAgentVersion { get; set; } = "";

    public int ComputerId { get; set; }

    public List<AgentCommand> Commands { get; set; } = [];
}

public class AgentCommand
{
    public int Id { get; set; }

    public string Type { get; set; } = "";
}