using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace JoyZoning.Tests.Infrastructure;

internal static class UnitTestServiceCollection
{
    public static IServiceCollection AddUnitTestHost(this IServiceCollection services)
    {
        services.AddLogging();
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        services.AddSingleton(env.Object);
        return services;
    }
}
