namespace ClientManagement.Server.Entities;

public sealed class AgentCommandEntity
{
    public long Id { get; set; }

    public required string AgentId { get; set; }

    public required string CommandType { get; set; }

    public required string Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public string? ResultMessage { get; set; }
}