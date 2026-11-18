namespace ClientManagement.Shared.Dtos;

public sealed class AgentCommandDto
{
    public long CommandId { get; init; }

    public required string Type { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}