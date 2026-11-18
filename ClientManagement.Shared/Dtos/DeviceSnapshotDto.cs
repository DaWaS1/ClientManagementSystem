using System;
using System.Collections.Generic;
using System.Text;

namespace ClientManagement.Shared.Dtos
{
    public sealed class DeviceSnapshotDto
    {
        public required string AgentId { get; init; }

        public required string Hostname { get; init; }

        public string? IpAddress { get; init; }

        public string? OperatingSystem { get; init; }

        public string? OperatingSystemVersion { get; init; }

        public string? Manufacturer { get; init; }

        public string? Model { get; init; }

        public string? SerialNumber { get; init; }

        public ulong TotalMemoryBytes { get; init; }

        public List<DiskInfoDto> Disks { get; init; } = [];

        public DateTime CapturedAtUtc { get; init; }
    }

}
