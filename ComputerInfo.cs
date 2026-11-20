
namespace LabMonitorAgent;

public class ComputerInfo(
    WindowsSessionMonitor sessionMonitor
)
{
    public string GetComputerName()
    {
        return Environment.MachineName;
    }

    public string? GetActiveUsername()
    {
        return sessionMonitor.GetActiveUsername();
    }
}
