using JoyZoning.Agents;
using JoyZoning.Agents.Hermes;
using JoyZoning.ControlPlane;
using JoyZoning.ControlPlane.Hubs;
using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Configuration;
using JoyZoning.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace JoyZoning.Tests.Infrastructure;

internal static class JoyZoningTestHostConfigurer
{
    public static void Configure(IWebHostBuilder builder, SqliteConnection sqlite, TestDietCodeAdapter dietCode)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JoyZoning:DatabasePath"] = sqlite.DataSource,
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<JoyZoningDbContext>>();
            services.RemoveAll<JoyZoningDbContext>();
            services.AddDbContext<JoyZoningDbContext>(options => options.UseSqlite(sqlite));

            services.RemoveAll<IHostedService>();
            services.RemoveAll<IHubContext<OperatorHub>>();
            services.AddSingleton<IHubContext<OperatorHub>, TestOperatorHubContext>();

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
