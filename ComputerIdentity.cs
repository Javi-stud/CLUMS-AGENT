namespace LabMonitorAgent;

public class ComputerIdentity
{
    private readonly string identityDirectory;
    private readonly string identityFilePath;

    public ComputerIdentity()
    {
        identityDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData
            ),
            "LabMonitorAgent"
        );

        identityFilePath = Path.Combine(
            identityDirectory,
            "computer-id.txt"
        );
    }

    public string GetOrCreateUuid()
    {
        Directory.CreateDirectory(identityDirectory);

        if (File.Exists(identityFilePath))
        {
            string savedUuid = File.ReadAllText(identityFilePath).Trim();

            if (Guid.TryParse(savedUuid, out Guid existingUuid))
            {
                return existingUuid.ToString();
            }
        }

        string newUuid = Guid.NewGuid().ToString();

        File.WriteAllText(identityFilePath, newUuid);

        return newUuid;
    }
}