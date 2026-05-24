using JoyZoning.Agents;
using JoyZoning.ControlPlane.Background;
using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class HermesRunEventConsumerTests
{
    [Fact]
    public void TrackRun_does_not_throw_when_starting_background_consumer()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var registry = new AgentAdapterRegistry(Array.Empty<IAgentAdapter>());
        var scopeFactory = new Mock<IServiceScopeFactory>();

        var consumer = new HermesRunEventConsumer(
            registry,
            scopeFactory.Object,
            env.Object,
            Options.Create(new ExecutorOptions()),
            NullLogger<HermesRunEventConsumer>.Instance);

        var ex = Record.Exception(() => consumer.TrackRun("run-1", AgentKind.DietCode, Guid.NewGuid()));

        Assert.Null(ex);
        consumer.StopTracking("run-1");
    }

    [Fact]
    public void TrackRun_is_noop_in_testing_environment()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Testing");

        var registry = new AgentAdapterRegistry(Array.Empty<IAgentAdapter>());
        var scopeFactory = new Mock<IServiceScopeFactory>();

        var consumer = new HermesRunEventConsumer(
            registry,
            scopeFactory.Object,
            env.Object,
            Options.Create(new ExecutorOptions()),
            NullLogger<HermesRunEventConsumer>.Instance);

        consumer.TrackRun("run-1", AgentKind.DietCode, Guid.NewGuid());

        scopeFactory.Verify(f => f.CreateScope(), Times.Never);
    }
}
