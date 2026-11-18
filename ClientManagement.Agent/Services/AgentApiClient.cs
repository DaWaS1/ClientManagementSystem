using System.Net;
using System.Net.Http.Json;
using ClientManagement.Shared.Dtos;

namespace ClientManagement.Agent.Services;

public sealed class AgentApiClient
{
    private const string ApiKeyHeaderName =
        "X-ClientManagement-Api-Key";

    private readonly HttpClient _httpClient;


    public AgentApiClient(
        HttpClient httpClient,
        string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException(
                "Az API-kulcs nem lehet üres.",
                nameof(apiKey));
        }

        _httpClient = httpClient;

        _httpClient.DefaultRequestHeaders.Remove(
            ApiKeyHeaderName);

        _httpClient.DefaultRequestHeaders.Add(
            ApiKeyHeaderName,
            apiKey);
    }


    // =====================================================
    // SNAPSHOT KÜLDÉSE
    // =====================================================

    public async Task SendSnapshotAsync(
        DeviceSnapshotDto snapshot,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response =
            await _httpClient.PostAsJsonAsync(
                "api/agents/snapshot",
                snapshot,
                cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string responseBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        throw new HttpRequestException(
            $"A snapshot küldése sikertelen. " +
            $"HTTP {(int)response.StatusCode}: " +
            $"{responseBody}");
    }


    // =====================================================
    // HEARTBEAT KÜLDÉSE
    // =====================================================

    public async Task SendHeartbeatAsync(
        AgentHeartbeatDto heartbeat,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response =
            await _httpClient.PostAsJsonAsync(
                "api/agents/heartbeat",
                heartbeat,
                cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string responseBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        throw new HttpRequestException(
            $"A heartbeat küldése sikertelen. " +
            $"HTTP {(int)response.StatusCode}: " +
            $"{responseBody}");
    }


    // =====================================================
    // KÖVETKEZŐ VÁRAKOZÓ PARANCS LEKÉRÉSE
    // =====================================================

    public async Task<AgentCommandDto?> GetNextCommandAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        string escapedAgentId =
            Uri.EscapeDataString(agentId);

        string requestUri =
            $"api/agents/{escapedAgentId}/commands/next";


        using HttpResponseMessage response =
            await _httpClient.GetAsync(
                requestUri,
                cancellationToken);


        // Nincs várakozó parancs.
        if (response.StatusCode ==
            HttpStatusCode.NoContent)
        {
            return null;
        }


        if (!response.IsSuccessStatusCode)
        {
            string responseBody =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            throw new HttpRequestException(
                $"A parancs lekérése sikertelen. " +
                $"HTTP {(int)response.StatusCode}: " +
                $"{responseBody}");
        }


        AgentCommandDto? command =
            await response.Content
                .ReadFromJsonAsync<AgentCommandDto>(
                    cancellationToken:
                        cancellationToken);


        if (command is null)
        {
            throw new InvalidOperationException(
                "A Server sikeres választ adott, " +
                "de a parancs nem olvasható be.");
        }


        return command;
    }


    // =====================================================
    // PARANCS EREDMÉNYÉNEK VISSZAJELZÉSE
    // =====================================================

    public async Task CompleteCommandAsync(
        string agentId,
        long commandId,
        bool success,
        string? message,
        CancellationToken cancellationToken = default)
    {
        string escapedAgentId =
            Uri.EscapeDataString(agentId);


        var result =
            new AgentCommandResultDto
            {
                Success =
                    success,

                Message =
                    message
            };


        string requestUri =
            $"api/agents/{escapedAgentId}/" +
            $"commands/{commandId}/complete";


        using HttpResponseMessage response =
            await _httpClient.PostAsJsonAsync(
                requestUri,
                result,
                cancellationToken);


        if (response.IsSuccessStatusCode)
        {
            return;
        }


        string responseBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);


        throw new HttpRequestException(
            $"A parancs eredményének visszaküldése sikertelen. " +
            $"HTTP {(int)response.StatusCode}: " +
            $"{responseBody}");
    }
}