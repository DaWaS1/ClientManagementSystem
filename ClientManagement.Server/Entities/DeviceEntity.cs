namespace ClientManagement.Server.Entities;

public sealed class DeviceEntity
{
    public int Id { get; set; }

    public string AgentId { get; set; } = string.Empty;

    public string Hostname { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public string? OperatingSystem { get; set; }

    public string? OperatingSystemVersion { get; set; }

    public string? Manufacturer { get; set; }

    public string? Model { get; set; }

    public string? SerialNumber { get; set; }

    public long TotalMemoryBytes { get; set; }

    public DateTime CapturedAtUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }

    public List<DiskEntity> Disks { get; set; } = [];
}