using JoyZoning.Agents.Hermes;
using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JoyZoning.Agents;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJoyZoningAgents(this IServiceCollection services)
    {
        services.AddSingleton<HermesRuntimeSettings>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HermesOptions>>().Value;
            return new HermesRuntimeSettings(opts);
        });
        services.AddHttpClient<HermesHttpClient>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
        });
        services.AddHttpClient(KanbanSyncService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(12);
        });
        services.AddHttpClient(HermesDashboardService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(12);
        });
        services.AddSingleton<HermesAdapter>();
        services.AddSingleton<DietCodeAdapter>();
        services.AddSingleton<IAgentAdapter>(sp => sp.GetRequiredService<HermesAdapter>());
        services.AddSingleton<IAgentAdapter>(sp => sp.GetRequiredService<DietCodeAdapter>());
        services.AddSingleton<AgentAdapterRegistry>(sp =>
            new AgentAdapterRegistry(sp.GetServices<IAgentAdapter>()));
        services.AddSingleton<HermesProcessService>();
        services.AddSingleton<HermesDashboardService>();
        services.AddSingleton<KanbanSyncService>();
        services.AddSingleton<HermesHabitatBridgeService>();
        return services;
    }

    public static IServiceCollection ConfigureHermes(this IServiceCollection services, Action<HermesOptions> configure)
    {
        services.Configure(configure);
        return services;
    }
}
