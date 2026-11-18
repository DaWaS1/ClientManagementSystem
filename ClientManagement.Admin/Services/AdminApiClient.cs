using System.Net.Http.Json;
using ClientManagement.Shared.Dtos;

namespace ClientManagement.Admin.Services;

public sealed class AdminApiClient
{
    private readonly HttpClient _httpClient;

    public AdminApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<DeviceOverviewDto>> GetDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        List<DeviceOverviewDto>? devices =
            await _httpClient.GetFromJsonAsync<List<DeviceOverviewDto>>(
                "api/admin/devices",
                cancellationToken);

        return devices ?? [];
    }

    public async Task<DeviceSnapshotDto?> GetDeviceAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        string escapedAgentId = Uri.EscapeDataString(agentId);

        return await _httpClient.GetFromJsonAsync<DeviceSnapshotDto>(
            $"api/admin/devices/{escapedAgentId}",
            cancellationToken);
    }

    public async Task<DeviceOverviewDto?> GetDeviceOverviewAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        List<DeviceOverviewDto> devices =
            await GetDevicesAsync(cancellationToken);

        return devices.FirstOrDefault(
            device =>
                device.AgentId.Equals(
                    agentId,
                    StringComparison.OrdinalIgnoreCase));
    }

    public async Task RequestSnapshotAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        string escapedAgentId = Uri.EscapeDataString(agentId);

        string requestUri =
            $"api/admin/devices/{escapedAgentId}/commands/request-snapshot";

        using HttpResponseMessage response =
            await _httpClient.PostAsync(
                requestUri,
                content: null,
                cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string responseBody =
            await response.Content.ReadAsStringAsync(cancellationToken);

        throw new HttpRequestException(
            $"A távoli adatfrissítés kérése sikertelen. " +
            $"HTTP {(int)response.StatusCode}: {responseBody}");
    }
}
