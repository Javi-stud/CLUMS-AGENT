namespace LabMonitorAgent;

public class ComputerInfo
{
    public string GetComputerName()
    {
        return Environment.MachineName;
    }
}