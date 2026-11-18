using ClientManagement.Agent.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder =
    Host.CreateApplicationBuilder(args);


// ==========================================
// WINDOWS SERVICE TÁMOGATÁS
// ==========================================

builder.Services.AddWindowsService(
    options =>
    {
        options.ServiceName =
            "ClientManagement Agent";
    });


// ==========================================
// SAJÁT SERVICE-EK
// ==========================================

builder.Services.AddSingleton<
    AgentIdentityProvider>();

builder.Services.AddSingleton<
    SystemInfoCollector>();

builder.Services.AddHostedService<
    AgentWorker>();


// ==========================================
// HOST INDÍTÁSA
// ==========================================

IHost host =
    builder.Build();

await host.RunAsync();