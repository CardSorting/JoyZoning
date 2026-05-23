using JoyZoning.ControlPlane.Background;
using JoyZoning.Domain.Enums;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class HermesRunEventRulesTests
{
    [Theory]
    [InlineData("message.complete", false)]
    [InlineData("run.completed", true)]
    [InlineData("run.failed", true)]
    public void DietCode_terminal_events(string eventType, bool terminal) =>
        Assert.Equal(terminal, HermesRunEventRules.IsTerminalEvent(eventType, AgentKind.DietCode));

    [Fact]
    public void Manager_treats_message_complete_as_terminal() =>
        Assert.True(HermesRunEventRules.IsTerminalEvent("message.complete", AgentKind.Hermes));

    [Theory]
    [InlineData("running", true)]
    [InlineData("completed", false)]
    [InlineData("awaiting_approval", true)]
    [InlineData("queued", true)]
    [InlineData("paused", true)]
    [InlineData("requires_action", true)]
    public void Active_run_status(string status, bool active) =>
        Assert.Equal(active, HermesRunEventRules.IsActiveRunStatus(status));
}
