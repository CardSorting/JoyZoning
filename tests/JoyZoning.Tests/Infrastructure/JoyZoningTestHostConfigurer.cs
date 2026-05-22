using JoyZoning.Agents;
using JoyZoning.Agents.Hermes;
using JoyZoning.ControlPlane;
using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace JoyZoning.Tests.Infrastructure;

internal static class JoyZoningTestHostConfigurer
{
    public static void Configure(IWebHostBuilder builder, string dbPath, TestDietCodeAdapter dietCode)
    {
        builder.UseSetting(WebHostDefaults.ApplicationKey, dbPath);
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JoyZoning:DatabasePath"] = dbPath,
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();

            services.RemoveAll<HermesAdapter>();
            services.RemoveAll<DietCodeAdapter>();
            services.RemoveAll<IAgentAdapter>();
            services.RemoveAll<AgentAdapterRegistry>();

            services.AddSingleton<IAgentAdapter, TestHermesAdapter>();
            services.AddSingleton<IAgentAdapter>(_ => dietCode);
            services.AddSingleton<AgentAdapterRegistry>(sp =>
                new AgentAdapterRegistry(sp.GetServices<IAgentAdapter>()));

            services.Configure<LeaseRuntimeOptions>(o =>
            {
                o.Stale.RunningMinutes = 1;
                o.Stale.LeasedMinutes = 1;
                o.ReconciliationIntervalSeconds = 15;
            });
        });
    }
}
