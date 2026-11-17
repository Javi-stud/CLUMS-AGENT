using System.Reflection;

namespace LabMonitorAgent;

public static class AgentInfo
{
    /// <summary>Version from the csproj (&lt;Version&gt;), e.g. "1.0.0".</summary>
    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
}