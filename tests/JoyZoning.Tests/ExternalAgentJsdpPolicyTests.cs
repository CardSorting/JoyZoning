using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

public class ExternalAgentJsdpPolicyTests
{
    [Fact]
    public void External_role_converged_when_complete_and_merge_flag_set()
    {
        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            TaskExecutionMode = TaskExecutionMode.ExternalAgent,
            Status = WorkTaskStatus.Complete,
            ExternalMergeCompleted = true,
        };

        Assert.True(JsdpMergeGate.IsRoleConverged(task, Array.Empty<ExecutionLease>()));
    }

    [Fact]
    public void Managed_role_still_requires_merged_lease()
    {
        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            Status = WorkTaskStatus.Complete,
            TaskExecutionMode = TaskExecutionMode.ManagedAgent,
        };

        Assert.False(JsdpMergeGate.IsRoleConverged(task, Array.Empty<ExecutionLease>()));
    }

    [Theory]
    [InlineData("cursor", ExecutionDriver.ExternalCursor)]
    [InlineData("claude-code", ExecutionDriver.ExternalClaudeCode)]
    [InlineData("manual", ExecutionDriver.ExternalManual)]
    public void Agent_mapping_resolves_driver(string agent, ExecutionDriver expected)
    {
        Assert.True(ExternalExecutionDriverMapping.TryParseAgent(agent, out var driver, out _));
        Assert.Equal(expected, driver);
    }

    [Fact]
    public void External_prompt_includes_jsdp_sections_and_scope()
    {
        var session = new OperatorSession
        {
            Id = Guid.NewGuid(),
            Name = "TinyQuest",
            WorkspaceRoot = "/tmp/ws",
            ExecutionMode = SessionExecutionMode.BoundedRole,
        };
        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            Title = "Role 1 — Product Lock",
            Description = "Lock product scope and goals for the app.",
        };

        var prompt = ExternalAgentPromptBuilder.Build(
            session.Name,
            task,
            session,
            "/tmp/ws",
            "joyzoning/card-abc",
            ExternalAgentPromptBuilder.DefaultAllowedAreas(task, session));

        Assert.Contains("JSDP", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Required output", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Product Lock", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("joyzoning/card-abc", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Jsdp_session_policy_recognizes_external_bounded_execution()
    {
        var session = new OperatorSession { ExecutionMode = SessionExecutionMode.BoundedRole };
        var task = new WorkTask { TaskExecutionMode = TaskExecutionMode.ExternalAgent };
        Assert.True(JsdpSessionPolicy.IsExternalBoundedExecution(task, session));
    }
}
