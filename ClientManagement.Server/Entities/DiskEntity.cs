namespace ClientManagement.Server.Entities;

public sealed class DiskEntity
{
    public int Id { get; set; }

    public int DeviceId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? VolumeLabel { get; set; }

    public long TotalSizeBytes { get; set; }

    public long FreeSpaceBytes { get; set; }

    public DeviceEntity Device { get; set; } = null!;
}