using System.Text.Json;
using ClientManagement.Agent.Configuration;
using ClientManagement.Shared.Commands;
using ClientManagement.Shared.Dtos;
using Microsoft.Extensions.Hosting;

namespace ClientManagement.Agent.Services;

public sealed class AgentWorker : BackgroundService
{
    private readonly AgentIdentityProvider _identityProvider;
    private readonly SystemInfoCollector _systemInfoCollector;


    public AgentWorker(
        AgentIdentityProvider identityProvider,
        SystemInfoCollector systemInfoCollector)
    {
        _identityProvider =
            identityProvider;

        _systemInfoCollector =
            systemInfoCollector;
    }


    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            AgentSettings settings =
                LoadSettings();


            string agentId =
                _identityProvider
                    .GetOrCreateAgentId();


            using var httpClient =
                new HttpClient
                {
                    BaseAddress =
                        new Uri(
                            settings.ServerBaseUrl),

                    Timeout =
                        TimeSpan.FromSeconds(10)
                };


            var apiClient =
                new AgentApiClient(
                    httpClient,
                    settings.ApiKey);


            TimeSpan heartbeatInterval =
                TimeSpan.FromSeconds(20);

            TimeSpan snapshotInterval =
                TimeSpan.FromMinutes(5);


            DateTime nextSnapshotUtc =
                DateTime.MinValue;


            AgentLogger.Write(
                "ClientManagement Agent elindult.");

            AgentLogger.Write(
                $"Agent ID: {agentId}");

            AgentLogger.Write(
                $"Hostname: {Environment.MachineName}");

            AgentLogger.Write(
                $"Heartbeat intervallum: " +
                $"{heartbeatInterval.TotalSeconds:N0} másodperc");

            AgentLogger.Write(
                $"Teljes adatküldés: " +
                $"{snapshotInterval.TotalMinutes:N0} perc");


            while (!stoppingToken.IsCancellationRequested)
            {
                DateTime nowUtc =
                    DateTime.UtcNow;


                // ==========================================
                // AUTOMATIKUS TELJES SNAPSHOT
                // ==========================================

                if (nowUtc >= nextSnapshotUtc)
                {
                    bool snapshotSuccess =
                        await TrySendSnapshotAsync(
                            apiClient,
                            agentId,
                            stoppingToken);


                    if (snapshotSuccess)
                    {
                        nextSnapshotUtc =
                            DateTime.UtcNow
                                .Add(snapshotInterval);
                    }
                }


                // ==========================================
                // HEARTBEAT
                // ==========================================

                await TrySendHeartbeatAsync(
                    apiClient,
                    agentId,
                    stoppingToken);


                // ==========================================
                // TÁVOLI PARANCS LEKÉRÉSE ÉS VÉGREHAJTÁSA
                // ==========================================

                bool remoteSnapshotWasExecuted =
                    await TryProcessNextCommandAsync(
                        apiClient,
                        agentId,
                        stoppingToken);


                // Ha távoli parancs miatt most küldtünk
                // teljes snapshotot, ne küldjünk néhány
                // másodperc múlva újabbat automatikusan.
                if (remoteSnapshotWasExecuted)
                {
                    nextSnapshotUtc =
                        DateTime.UtcNow
                            .Add(snapshotInterval);
                }


                // ==========================================
                // VÁRAKOZÁS
                // ==========================================

                try
                {
                    await Task.Delay(
                        heartbeatInterval,
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }


            AgentLogger.Write(
                "ClientManagement Agent leáll.");
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            AgentLogger.Write(
                "ClientManagement Agent leáll.");
        }
        catch (Exception exception)
        {
            AgentLogger.Write(
                $"Kritikus Agent hiba: {exception}");

            throw;
        }
    }


    // =====================================================
    // SNAPSHOT KÜLDÉS
    // =====================================================

    private async Task<bool> TrySendSnapshotAsync(
        AgentApiClient apiClient,
        string agentId,
        CancellationToken cancellationToken)
    {
        try
        {
            AgentLogger.Write(
                "Rendszeradatok gyűjtése...");


            DeviceSnapshotDto snapshot =
                _systemInfoCollector.Collect(
                    agentId);


            await apiClient.SendSnapshotAsync(
                snapshot,
                cancellationToken);


            AgentLogger.Write(
                "Snapshot sikeresen elküldve.");


            return true;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            AgentLogger.Write(
                $"Snapshot kommunikációs hiba: " +
                $"{exception.Message}");

            return false;
        }
        catch (Exception exception)
        {
            AgentLogger.Write(
                $"Snapshot hiba: {exception}");

            return false;
        }
    }


    // =====================================================
    // HEARTBEAT
    // =====================================================

    private static async Task TrySendHeartbeatAsync(
        AgentApiClient apiClient,
        string agentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var heartbeat =
                new AgentHeartbeatDto
                {
                    AgentId =
                        agentId
                };


            await apiClient.SendHeartbeatAsync(
                heartbeat,
                cancellationToken);


            AgentLogger.Write(
                "Heartbeat OK");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            AgentLogger.Write(
                $"Heartbeat kommunikációs hiba: " +
                $"{exception.Message}");
        }
        catch (Exception exception)
        {
            AgentLogger.Write(
                $"Heartbeat hiba: {exception}");
        }
    }


    // =====================================================
    // KÖVETKEZŐ PARANCS FELDOLGOZÁSA
    // =====================================================

    private async Task<bool> TryProcessNextCommandAsync(
        AgentApiClient apiClient,
        string agentId,
        CancellationToken cancellationToken)
    {
        AgentCommandDto? command;


        // ------------------------------------------
        // Parancs lekérése
        // ------------------------------------------

        try
        {
            command =
                await apiClient.GetNextCommandAsync(
                    agentId,
                    cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            AgentLogger.Write(
                $"Parancs lekérési hiba: " +
                $"{exception.Message}");

            return false;
        }
        catch (Exception exception)
        {
            AgentLogger.Write(
                $"Parancs lekérési hiba: " +
                $"{exception}");

            return false;
        }


        // Nincs függő parancs.
        if (command is null)
        {
            return false;
        }


        AgentLogger.Write(
            $"Parancs érkezett: " +
            $"ID={command.CommandId}, " +
            $"Type={command.Type}");


        bool commandSuccess;
        string resultMessage;
        bool snapshotWasExecuted = false;


        // ------------------------------------------
        // Parancs végrehajtása
        // ------------------------------------------

        try
        {
            switch (command.Type)
            {
                case AgentCommandTypes.RequestSnapshot:
                    {
                        AgentLogger.Write(
                            $"Távoli snapshot kérés végrehajtása. " +
                            $"Command ID: {command.CommandId}");


                        bool snapshotSuccess =
                            await TrySendSnapshotAsync(
                                apiClient,
                                agentId,
                                cancellationToken);


                        if (snapshotSuccess)
                        {
                            commandSuccess =
                                true;

                            resultMessage =
                                "A teljes rendszeradat-snapshot " +
                                "sikeresen elküldve.";

                            snapshotWasExecuted =
                                true;
                        }
                        else
                        {
                            commandSuccess =
                                false;

                            resultMessage =
                                "A snapshot elküldése sikertelen.";
                        }

                        break;
                    }


                default:
                    {
                        commandSuccess =
                            false;

                        resultMessage =
                            $"Ismeretlen parancstípus: " +
                            $"{command.Type}";

                        AgentLogger.Write(
                            resultMessage);

                        break;
                    }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            commandSuccess =
                false;

            resultMessage =
                exception.Message;

            AgentLogger.Write(
                $"Parancs végrehajtási hiba: " +
                $"{exception}");
        }


        // ------------------------------------------
        // Eredmény visszaküldése
        // ------------------------------------------

        try
        {
            await apiClient.CompleteCommandAsync(
                agentId,
                command.CommandId,
                commandSuccess,
                resultMessage,
                cancellationToken);


            AgentLogger.Write(
                $"Parancs lezárva: " +
                $"ID={command.CommandId}, " +
                $"Success={commandSuccess}");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            AgentLogger.Write(
                $"A parancs lezárása nem sikerült: " +
                $"{exception.Message}");
        }
        catch (Exception exception)
        {
            AgentLogger.Write(
                $"A parancs lezárása nem sikerült: " +
                $"{exception}");
        }


        return snapshotWasExecuted;
    }


    // =====================================================
    // KONFIGURÁCIÓ
    // =====================================================

    private static AgentSettings LoadSettings()
    {
        string settingsPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "agentsettings.json");


        if (!File.Exists(settingsPath))
        {
            throw new FileNotFoundException(
                "Az Agent konfigurációs fájl nem található.",
                settingsPath);
        }


        string settingsJson =
            File.ReadAllText(
                settingsPath);


        AgentSettings? settings =
            JsonSerializer.Deserialize<AgentSettings>(
                settingsJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive =
                        true
                });


        if (settings is null)
        {
            throw new InvalidOperationException(
                "Az Agent konfiguráció nem olvasható be.");
        }


        if (string.IsNullOrWhiteSpace(
            settings.ServerBaseUrl))
        {
            throw new InvalidOperationException(
                "A ServerBaseUrl nincs konfigurálva.");
        }


        if (!Uri.TryCreate(
            settings.ServerBaseUrl,
            UriKind.Absolute,
            out _))
        {
            throw new InvalidOperationException(
                "A ServerBaseUrl nem érvényes URL.");
        }


        if (string.IsNullOrWhiteSpace(
            settings.ApiKey))
        {
            throw new InvalidOperationException(
                "Az Agent API-kulcs nincs konfigurálva.");
        }


        return settings;
    }
}