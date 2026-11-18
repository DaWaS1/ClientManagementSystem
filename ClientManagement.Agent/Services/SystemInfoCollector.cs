using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using ClientManagement.Shared.Dtos;

namespace ClientManagement.Agent.Services;

public sealed class SystemInfoCollector
{
    public DeviceSnapshotDto Collect(string agentId)
    {
        Dictionary<string, string?> computerSystem = QuerySingleWmiObject(
            "SELECT Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem",
            "Manufacturer",
            "Model",
            "TotalPhysicalMemory");

        Dictionary<string, string?> operatingSystem = QuerySingleWmiObject(
            "SELECT Caption, Version FROM Win32_OperatingSystem",
            "Caption",
            "Version");

        Dictionary<string, string?> bios = QuerySingleWmiObject(
            "SELECT SerialNumber FROM Win32_BIOS",
            "SerialNumber");

        return new DeviceSnapshotDto
        {
            // Első verzióban a gépnév az azonosító.
            // Később ezt tartósan eltárolt GUID-ra cseréljük.
            AgentId = agentId,

            Hostname = Environment.MachineName,
            IpAddress = GetPrimaryIpv4Address(),

            OperatingSystem = operatingSystem.GetValueOrDefault("Caption"),
            OperatingSystemVersion = operatingSystem.GetValueOrDefault("Version"),

            Manufacturer = computerSystem.GetValueOrDefault("Manufacturer"),
            Model = computerSystem.GetValueOrDefault("Model"),
            SerialNumber = bios.GetValueOrDefault("SerialNumber")?.Trim(),

            TotalMemoryBytes = ParseUnsignedLong(
                computerSystem.GetValueOrDefault("TotalPhysicalMemory")),

            Disks = GetDiskInformation(),

            CapturedAtUtc = DateTime.UtcNow
        };
    }

    private static Dictionary<string, string?> QuerySingleWmiObject(
        string query,
        params string[] propertyNames)
    {
        var values = propertyNames.ToDictionary(
            propertyName => propertyName,
            _ => (string?)null);

        using var searcher = new ManagementObjectSearcher(query);
        using ManagementObjectCollection results = searcher.Get();

        foreach (ManagementObject item in results)
        {
            using (item)
            {
                foreach (string propertyName in propertyNames)
                {
                    values[propertyName] = item[propertyName]?.ToString();
                }
            }

            break;
        }

        return values;
    }

    private static string? GetPrimaryIpv4Address()
    {
        NetworkInterface? networkInterface = NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(network =>
                network.OperationalStatus == OperationalStatus.Up &&
                network.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                network.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .OrderByDescending(network =>
                network.GetIPProperties().GatewayAddresses.Count > 0)
            .FirstOrDefault(network =>
                network.GetIPProperties()
                    .UnicastAddresses
                    .Any(address =>
                        address.Address.AddressFamily == AddressFamily.InterNetwork));

        return networkInterface?
            .GetIPProperties()
            .UnicastAddresses
            .FirstOrDefault(address =>
                address.Address.AddressFamily == AddressFamily.InterNetwork)?
            .Address
            .ToString();
    }

    private static List<DiskInfoDto> GetDiskInformation()
    {
        var disks = new List<DiskInfoDto>();

        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady || drive.DriveType != DriveType.Fixed)
            {
                continue;
            }

            disks.Add(new DiskInfoDto
            {
                Name = drive.Name,
                VolumeLabel = drive.VolumeLabel,
                TotalSizeBytes = drive.TotalSize,
                FreeSpaceBytes = drive.AvailableFreeSpace
            });
        }

        return disks;
    }

    private static ulong ParseUnsignedLong(string? value)
    {
        return ulong.TryParse(value, out ulong parsedValue)
            ? parsedValue
            : 0;
    }
}