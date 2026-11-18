namespace ClientManagement.Agent.Services;

public sealed class AgentIdentityProvider
{
    private readonly string _identityFilePath;

    public AgentIdentityProvider()
    {
        string applicationDataPath =
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData);

        string agentDataDirectory =
            Path.Combine(
                applicationDataPath,
                "ClientManagement",
                "Agent");

        _identityFilePath =
            Path.Combine(
                agentDataDirectory,
                "agent-id.txt");
    }


    public string GetOrCreateAgentId()
    {
        string? directory =
            Path.GetDirectoryName(
                _identityFilePath);

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(
                "Az Agent azonosító könyvtára nem határozható meg.");
        }

        Directory.CreateDirectory(directory);


        // Ha már létezik az azonosító, azt használjuk.
        if (File.Exists(_identityFilePath))
        {
            string existingAgentId =
                File.ReadAllText(
                    _identityFilePath)
                    .Trim();

            if (Guid.TryParse(
                existingAgentId,
                out Guid existingGuid))
            {
                return existingGuid.ToString("D");
            }

            throw new InvalidDataException(
                $"Az Agent azonosító fájl tartalma érvénytelen: {_identityFilePath}");
        }


        // Első induláskor új GUID létrehozása.
        string newAgentId =
            Guid.NewGuid().ToString("D");

        File.WriteAllText(
            _identityFilePath,
            newAgentId);

        return newAgentId;
    }
}