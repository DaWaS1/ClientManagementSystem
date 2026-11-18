namespace ClientManagement.Shared.Dtos;

public sealed class AgentCommandResultDto
{
    public bool Success { get; init; }

    public string? Message { get; init; }
}