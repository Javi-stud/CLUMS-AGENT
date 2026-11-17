namespace LabMonitorAgent;

public class ComputerInfo
{
    public string GetComputerName()
    {
        return Environment.MachineName;
    }
    
    public string GetLoggedInUsername()
    {
        return Environment.UserName;
    }
}