using ClientManagement.Server.Data;
using ClientManagement.Server.Entities;
using ClientManagement.Shared.Dtos;
using ClientManagement.Shared.Commands;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

const string AgentApiKeyHeaderName =
    "X-ClientManagement-Api-Key";

string? configuredAgentApiKey =
    builder.Configuration[
        "AgentAuthentication:ApiKey"];

if (string.IsNullOrWhiteSpace(
    configuredAgentApiKey))
{
    throw new InvalidOperationException(
        "Az Agent API-kulcs nincs konfigurálva.");
}

string agentApiKey =
    configuredAgentApiKey.Trim();


// ----------------------------------------------------
// SQLite adatbázis helyének meghatározása
// ----------------------------------------------------

string dataDirectory = Path.Combine(
    builder.Environment.ContentRootPath,
    "Data");

Directory.CreateDirectory(dataDirectory);

string databasePath = Path.Combine(
    dataDirectory,
    "clientmanagement.db");


// ----------------------------------------------------
// Entity Framework Core + SQLite
// ----------------------------------------------------

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite($"Data Source={databasePath}");
});


var app = builder.Build();


// ----------------------------------------------------
// Migrációk automatikus alkalmazása induláskor
// ----------------------------------------------------

using (IServiceScope scope = app.Services.CreateScope())
{
    AppDbContext database =
        scope.ServiceProvider.GetRequiredService<AppDbContext>();

    database.Database.Migrate();
}

// ============================================================
// Agent API-kulcs hitelesítés
// ============================================================

app.Use(
    async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments(
            "/api/agents"))
        {
            if (!context.Request.Headers.TryGetValue(
                AgentApiKeyHeaderName,
                out var providedApiKeyValues))
            {
                context.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;

                await context.Response.WriteAsJsonAsync(
                    new
                    {
                        error =
                            "Hiányzó Agent API-kulcs."
                    });

                return;
            }


            string providedApiKey =
                providedApiKeyValues.ToString();


            if (!string.Equals(
                providedApiKey,
                agentApiKey,
                StringComparison.Ordinal))
            {
                context.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;

                await context.Response.WriteAsJsonAsync(
                    new
                    {
                        error =
                            "Érvénytelen Agent API-kulcs."
                    });

                return;
            }
        }


        await next();
    });



// ----------------------------------------------------
// Teszt végpont
// ----------------------------------------------------

app.MapGet("/", () => Results.Ok(new
{
    message = "ClientManagement.Server működik.",
    serverTimeUtc = DateTime.UtcNow
}));


// ----------------------------------------------------
// AGENT -> SERVER
// Snapshot fogadása
// ----------------------------------------------------

app.MapPost(
    "/api/agents/snapshot",
    async (
        DeviceSnapshotDto snapshot,
        AppDbContext database,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(snapshot.AgentId))
        {
            return Results.BadRequest(new
            {
                error = "Az AgentId megadása kötelező."
            });
        }

        if (string.IsNullOrWhiteSpace(snapshot.Hostname))
        {
            return Results.BadRequest(new
            {
                error = "A Hostname megadása kötelező."
            });
        }


        // Megnézzük, hogy létezik-e már ez az Agent.
        DeviceEntity? device = await database.Devices
            .Include(device => device.Disks)
            .SingleOrDefaultAsync(
                device => device.AgentId == snapshot.AgentId,
                cancellationToken);


        // Ha még nincs adatbázisban, létrehozzuk.
        if (device is null)
        {
            device = new DeviceEntity
            {
                AgentId = snapshot.AgentId
            };

            database.Devices.Add(device);
        }
        else
        {
            // A korábbi meghajtóadatokat friss adatokra cseréljük.
            database.Disks.RemoveRange(device.Disks);
            device.Disks.Clear();
        }


        // Alapadatok frissítése
        device.Hostname = snapshot.Hostname;
        device.IpAddress = snapshot.IpAddress;

        device.OperatingSystem =
            snapshot.OperatingSystem;

        device.OperatingSystemVersion =
            snapshot.OperatingSystemVersion;

        device.Manufacturer =
            snapshot.Manufacturer;

        device.Model =
            snapshot.Model;

        device.SerialNumber =
            snapshot.SerialNumber;

        device.TotalMemoryBytes =
            snapshot.TotalMemoryBytes > long.MaxValue
                ? long.MaxValue
                : (long)snapshot.TotalMemoryBytes;

        device.CapturedAtUtc =
            snapshot.CapturedAtUtc;

        device.LastSeenUtc =
            DateTime.UtcNow;


        // Meghajtók mentése
        foreach (DiskInfoDto disk in snapshot.Disks)
        {
            device.Disks.Add(new DiskEntity
            {
                Name = disk.Name,
                VolumeLabel = disk.VolumeLabel,
                TotalSizeBytes = disk.TotalSizeBytes,
                FreeSpaceBytes = disk.FreeSpaceBytes
            });
        }


        // FONTOS:
        // Itt kerül ténylegesen adatbázisba.
        await database.SaveChangesAsync(cancellationToken);


        Console.WriteLine(
            $"Snapshot ADATBÁZISBA mentve: " +
            $"{device.AgentId} - {device.CapturedAtUtc:O}");


        return Results.Ok(new
        {
            accepted = true,
            agentId = device.AgentId,
            receivedAtUtc = device.LastSeenUtc
        });
    });


// ----------------------------------------------------
// AGENT -> SERVER
// Heartbeat
// ----------------------------------------------------

app.MapPost(
    "/api/agents/heartbeat",
    async (
        AgentHeartbeatDto heartbeat,
        AppDbContext database,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(heartbeat.AgentId))
        {
            return Results.BadRequest(new
            {
                error = "Az AgentId megadása kötelező."
            });
        }

        DeviceEntity? device =
            await database.Devices
                .SingleOrDefaultAsync(
                    device =>
                        device.AgentId == heartbeat.AgentId,
                    cancellationToken);

        if (device is null)
        {
            return Results.NotFound(new
            {
                error = $"Nem található ilyen agent: {heartbeat.AgentId}"
            });
        }

        device.LastSeenUtc = DateTime.UtcNow;

        await database.SaveChangesAsync(cancellationToken);

        Console.WriteLine(
            $"Heartbeat: {device.AgentId} - {device.LastSeenUtc:O}");

        return Results.Ok(new
        {
            accepted = true,
            agentId = device.AgentId,
            receivedAtUtc = device.LastSeenUtc
        });
    });





// ----------------------------------------------------
// ADMIN -> SERVER
// Összes gép lekérése
// ----------------------------------------------------

app.MapGet(
    "/api/admin/devices",
    async (
        AppDbContext database,
        CancellationToken cancellationToken) =>
    {
        List<DeviceEntity> devices =
            await database.Devices
                .AsNoTracking()
                .Include(device => device.Disks)
                .OrderBy(device => device.Hostname)
                .ToListAsync(cancellationToken);

        DateTime nowUtc = DateTime.UtcNow;

        TimeSpan onlineTimeout =
            TimeSpan.FromSeconds(60);

        DeviceOverviewDto[] result =
            devices
                .Select(device => new DeviceOverviewDto
                {
                    AgentId = device.AgentId,

                    Hostname = device.Hostname,

                    IpAddress = device.IpAddress,

                    OperatingSystem =
                        device.OperatingSystem,

                    OperatingSystemVersion =
                        device.OperatingSystemVersion,

                    Manufacturer =
                        device.Manufacturer,

                    Model =
                        device.Model,

                    TotalMemoryBytes =
                        device.TotalMemoryBytes < 0
                            ? 0
                            : (ulong)device.TotalMemoryBytes,

                    CapturedAtUtc =
                        device.CapturedAtUtc,

                    LastSeenUtc =
                        device.LastSeenUtc,

                    IsOnline =
                        nowUtc - device.LastSeenUtc
                        <= onlineTimeout,

                    Disks =
                        device.Disks
                            .OrderBy(disk => disk.Name)
                            .Select(disk => new DiskInfoDto
                            {
                                Name = disk.Name,
                                VolumeLabel = disk.VolumeLabel,
                                TotalSizeBytes = disk.TotalSizeBytes,
                                FreeSpaceBytes = disk.FreeSpaceBytes
                            })
                            .ToList()
                })
                .ToArray();

        return Results.Ok(result);
    });

// ----------------------------------------------------
// ADMIN -> SERVER
// Egy adott gép lekérése
// ----------------------------------------------------

app.MapGet(
    "/api/admin/devices/{agentId}",
    async (
        string agentId,
        AppDbContext database,
        CancellationToken cancellationToken) =>
    {
        DeviceEntity? device =
            await database.Devices
                .AsNoTracking()
                .Include(device => device.Disks)
                .SingleOrDefaultAsync(
                    device => device.AgentId == agentId,
                    cancellationToken);


        if (device is null)
        {
            return Results.NotFound(new
            {
                error = $"Nem található ilyen agent: {agentId}"
            });
        }


        return Results.Ok(MapToDto(device));
    });


app.MapPost(
    "/api/admin/devices/{agentId}/commands/request-snapshot",
    async (
        string agentId,
        AppDbContext database,
        CancellationToken cancellationToken) =>
    {
        bool deviceExists =
            await database.Devices
                .AnyAsync(
                    device =>
                        device.AgentId == agentId,
                    cancellationToken);


        if (!deviceExists)
        {
            return Results.NotFound(
                new
                {
                    error =
                        "A megadott Agent nem található."
                });
        }


        AgentCommandEntity? existingCommand =
            await database.AgentCommands
                .Where(
                    command =>
                        command.AgentId == agentId &&
                        command.CommandType ==
                            AgentCommandTypes.RequestSnapshot &&
                        command.Status == "Pending")
                .OrderBy(
                    command =>
                        command.CreatedAtUtc)
                .FirstOrDefaultAsync(
                    cancellationToken);


        // Ha már várakozik ugyanez a parancs,
        // nem hozunk létre még egyet.
        if (existingCommand is not null)
        {
            return Results.Accepted(
                value: new
                {
                    commandId =
                        existingCommand.Id,

                    status =
                        existingCommand.Status,

                    alreadyPending =
                        true
                });
        }


        var command =
            new AgentCommandEntity
            {
                AgentId =
                    agentId,

                CommandType =
                    AgentCommandTypes.RequestSnapshot,

                Status =
                    "Pending",

                CreatedAtUtc =
                    DateTime.UtcNow
            };


        database.AgentCommands.Add(
            command);


        await database.SaveChangesAsync(
            cancellationToken);


        Console.WriteLine(
            $"Parancs létrehozva: " +
            $"{command.Id} | " +
            $"{command.AgentId} | " +
            $"{command.CommandType}");


        return Results.Accepted(
            value: new
            {
                commandId =
                    command.Id,

                status =
                    command.Status,

                alreadyPending =
                    false
            });
    });


app.MapGet(
    "/api/agents/{agentId}/commands/next",
    async (
        string agentId,
        AppDbContext database,
        CancellationToken cancellationToken) =>
    {
        AgentCommandEntity? command =
            await database.AgentCommands
                .AsNoTracking()
                .Where(
                    command =>
                        command.AgentId == agentId &&
                        command.Status == "Pending")
                .OrderBy(
                    command =>
                        command.CreatedAtUtc)
                .FirstOrDefaultAsync(
                    cancellationToken);


        if (command is null)
        {
            return Results.NoContent();
        }


        var dto =
            new AgentCommandDto
            {
                CommandId =
                    command.Id,

                Type =
                    command.CommandType,

                CreatedAtUtc =
                    command.CreatedAtUtc
            };


        return Results.Ok(dto);
    });

app.MapPost(
    "/api/agents/{agentId}/commands/{commandId:long}/complete",
    async (
        string agentId,
        long commandId,
        AgentCommandResultDto result,
        AppDbContext database,
        CancellationToken cancellationToken) =>
    {
        AgentCommandEntity? command =
            await database.AgentCommands
                .SingleOrDefaultAsync(
                    storedCommand =>
                        storedCommand.Id == commandId &&
                        storedCommand.AgentId == agentId,
                    cancellationToken);


        if (command is null)
        {
            return Results.NotFound(
                new
                {
                    error =
                        "A parancs nem található."
                });
        }


        // Ha már befejezett parancs, nem módosítjuk újra.
        if (command.Status != "Pending")
        {
            return Results.Ok(
                new
                {
                    commandId =
                        command.Id,

                    status =
                        command.Status,

                    alreadyCompleted =
                        true
                });
        }


        command.Status =
            result.Success
                ? "Completed"
                : "Failed";


        command.CompletedAtUtc =
            DateTime.UtcNow;


        command.ResultMessage =
            result.Message;


        await database.SaveChangesAsync(
            cancellationToken);


        Console.WriteLine(
            $"Parancs befejezve: " +
            $"{command.Id} | " +
            $"{command.AgentId} | " +
            $"{command.Status}");


        return Results.Ok(
            new
            {
                commandId =
                    command.Id,

                status =
                    command.Status,

                alreadyCompleted =
                    false
            });
    });


// ----------------------------------------------------
// DEBUG
// Megmutatja, mit lát az adatbázisban
// ----------------------------------------------------

app.MapGet(
    "/api/debug/database",
    async (
        AppDbContext database,
        CancellationToken cancellationToken) =>
    {
        int deviceCount =
            await database.Devices.CountAsync(cancellationToken);

        int diskCount =
            await database.Disks.CountAsync(cancellationToken);

        string databaseFile =
            database.Database.GetDbConnection().DataSource;


        return Results.Ok(new
        {
            databaseFile,
            deviceCount,
            diskCount
        });
    });


app.Run("http://0.0.0.0:5080");


// ----------------------------------------------------
// DeviceEntity -> DeviceSnapshotDto
// ----------------------------------------------------

static DeviceSnapshotDto MapToDto(DeviceEntity device)
{
    return new DeviceSnapshotDto
    {
        AgentId = device.AgentId,

        Hostname = device.Hostname,

        IpAddress = device.IpAddress,

        OperatingSystem =
            device.OperatingSystem,

        OperatingSystemVersion =
            device.OperatingSystemVersion,

        Manufacturer =
            device.Manufacturer,

        Model =
            device.Model,

        SerialNumber =
            device.SerialNumber,

        TotalMemoryBytes =
            device.TotalMemoryBytes < 0
                ? 0
                : (ulong)device.TotalMemoryBytes,

        CapturedAtUtc =
            device.CapturedAtUtc,

        Disks =
            device.Disks
                .OrderBy(disk => disk.Name)
                .Select(disk => new DiskInfoDto
                {
                    Name = disk.Name,
                    VolumeLabel = disk.VolumeLabel,
                    TotalSizeBytes = disk.TotalSizeBytes,
                    FreeSpaceBytes = disk.FreeSpaceBytes
                })
                .ToList()
    };
}