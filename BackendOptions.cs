namespace LabMonitorAgent;

/// <summary>Where the CLUMS server is and how to prove who we are. Bound from the "Backend" section.</summary>
public class BackendOptions
{
    public const string SectionName = "Backend";

    /// <summary>e.g. http://192.168.1.10:8000 (use https:// outside a closed lab network).</summary>
    public string BaseUrl { get; set; } = "http://localhost:8000";

    /// <summary>Must equal AGENT_API_KEY in the backend's .env. Never put the real value in git.</summary>
    public string AgentApiKey { get; set; } = "";

    public int RequestTimeoutSeconds { get; set; } = 10;
}