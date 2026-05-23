using JoyZoning.Adapters.Workspace;
using JoyZoning.Domain.Orchestration;
using Microsoft.Extensions.DependencyInjection;

namespace JoyZoning.Adapters;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJoyZoningAdapters(this IServiceCollection services)
    {
        services.AddSingleton<IWorkspaceAdapter, LocalWorkspaceAdapter>();
        services.AddSingleton<IWorkspaceGitMerger, WorkspaceGitMerger>();
        return services;
    }
}
