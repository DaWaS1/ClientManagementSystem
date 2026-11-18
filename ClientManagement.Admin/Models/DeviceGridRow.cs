namespace ClientManagement.Admin.Models;

public sealed class DeviceGridRow
{
    public string Status { get; init; } =
        string.Empty;

    public string AgentId { get; init; } =
        string.Empty;

    public string Hostname { get; init; } =
        string.Empty;

    public string IpAddress { get; init; } =
        string.Empty;

    public string OperatingSystem { get; init; } =
        string.Empty;

    public string Manufacturer { get; init; } =
        string.Empty;

    public string Model { get; init; } =
        string.Empty;

    public string Memory { get; init; } =
        string.Empty;

    public string Disks { get; init; } =
        string.Empty;

    public DateTime LastUpdate { get; init; }
}