namespace LabMonitorAgent;

/// <summary>Event names the server understands.</summary>
public static class AgentEventTypes
{
    public const string AgentStarted = "AGENT_STARTED";
    public const string AgentStopped = "AGENT_STOPPED";
    public const string StudentLogin = "STUDENT_LOGIN";     // needs ComputerUsername
    public const string StudentLogout = "STUDENT_LOGOUT";
    public const string AppStarted = "APP_STARTED";         // needs AppName
    public const string AppStopped = "APP_STOPPED";         // needs AppName
    public const string SystemShutdown = "SYSTEM_SHUTDOWN";
    public const string SystemRestart = "SYSTEM_RESTART";
}

/// <summary>
/// Something that happened on this PC. The id is created once, so re-sending after a
/// network failure is safe: the server ignores ids it has already seen.
/// </summary>
public class AgentEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public string? ComputerUsername { get; set; }
    public string? AppName { get; set; }

    public static AgentEvent Create(string type, string? computerUsername = null, string? appName = null)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        // Whole milliseconds only, so every JSON parser reads the timestamp exactly.
        now = new DateTimeOffset(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, now.Offset);

        return new AgentEvent
        {
            Type = type,
            OccurredAt = now,
            ComputerUsername = computerUsername,
            AppName = appName
        };
    }
}