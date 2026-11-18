namespace ClientManagement.Agent.Configuration;

public sealed class AgentSettings
{
    public string ServerBaseUrl { get; init; } =
        string.Empty;

    public string ApiKey { get; init; } =
        string.Empty;
}