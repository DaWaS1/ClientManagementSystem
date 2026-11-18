using System;
using System.Collections.Generic;
using System.Text;

namespace ClientManagement.Shared.Dtos
{
    public sealed class DiskInfoDto
    {
        public required string Name { get; init; }

        public string? VolumeLabel { get; init; }

        public long TotalSizeBytes { get; init; }

        public long FreeSpaceBytes { get; init; }
    }

}
