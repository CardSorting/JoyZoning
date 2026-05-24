using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class BoundedSessionGateTests
{
    private static readonly LeaseRuntimeOptions DefaultOptions = new() { MaxActiveLeasesPerSession = 1 };

    private static OperatorSession Session(bool boundedRole = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            ExecutionMode = boundedRole ? SessionExecutionMode.BoundedRole : SessionExecutionMode.Default,
            DeliveryChainId = boundedRole ? Guid.NewGuid() : null,
            DeliverySequence = boundedRole ? 1 : null,
            WorkspaceRoot = "/tmp/session",
        };

    private static WorkTask Task(string title, WorkTaskStatus status = WorkTaskStatus.Planned) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = title,
            Status = status,
        };

    [Fact]
    public void Blocks_second_role_while_first_in_progress()
    {
        var session = Session();
        var role1 = Task("Role 1 — Product Architect", WorkTaskStatus.InProgress);
        var role2 = Task("Role 2 — Mobile Architecture Lead");

        var error = BoundedSessionGate.ValidateDispatch(session, role2, [role1, role2], [], DefaultOptions);

        Assert.NotNull(error);
        Assert.Contains("Single-role session", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Bounded_role_session_rejects_second_task()
    {
        var session = Session(boundedRole: true);
        var role1 = Task("Role 1 — Product Architect");
        var role2 = Task("Role 2 — Mobile Architecture Lead");

        var error = BoundedSessionGate.ValidateDispatch(session, role2, [role1, role2], [], DefaultOptions);

        Assert.NotNull(error);
        Assert.Contains("exactly one task", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Default_max_active_leases_per_session_is_one()
    {
        Assert.Equal(1, new LeaseRuntimeOptions().MaxActiveLeasesPerSession);
    }
}

[Trait(TestCategories.Key, TestCategories.Unit)]
public class RoleDeliveryChainGateTests
{
    private static readonly LeaseRuntimeOptions DefaultOptions = new() { MaxActiveLeasesPerSession = 1 };

    [Fact]
    public void Blocks_sequence_2_until_sequence_1_complete()
    {
        var chainId = Guid.NewGuid();
        var session1 = BoundedSession(chainId, 1, "s1");
        var session2 = BoundedSession(chainId, 2, "s2");
        var task1 = RoleTask(session1.Id, "Role 1 — Product Architect", WorkTaskStatus.InProgress);
        var task2 = RoleTask(session2.Id, "Role 2 — Mobile Architecture Lead", WorkTaskStatus.Planned);

        var error = RoleDeliveryChainGate.ValidateDispatch(
            session2, task2, [session1, session2], [task1, task2], [], DefaultOptions);

        Assert.NotNull(error);
        Assert.Contains("merge gate closed", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sequence 1", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Allows_sequence_2_when_sequence_1_converged()
    {
        var chainId = Guid.NewGuid();
        var session1 = BoundedSession(chainId, 1, "s1");
        var session2 = BoundedSession(chainId, 2, "s2");
        var task1 = RoleTask(session1.Id, "Role 1 — Product Architect", WorkTaskStatus.Complete);
        var task2 = RoleTask(session2.Id, "Role 2 — Mobile Architecture Lead", WorkTaskStatus.Planned);
        var merged = JsdpTestFixtures.MergedLease(task1.Id, session1.Id);

        var error = RoleDeliveryChainGate.ValidateDispatch(
            session2, task2, [session1, session2], [task1, task2], [merged], DefaultOptions);

        Assert.Null(error);
    }

    [Fact]
    public void Blocks_sequence_2_when_sequence_1_complete_without_merge()
    {
        var chainId = Guid.NewGuid();
        var session1 = BoundedSession(chainId, 1, "s1");
        var session2 = BoundedSession(chainId, 2, "s2");
        var task1 = RoleTask(session1.Id, "Role 1 — Product Architect", WorkTaskStatus.Complete);
        var task2 = RoleTask(session2.Id, "Role 2 — Mobile Architecture Lead", WorkTaskStatus.Planned);

        var error = RoleDeliveryChainGate.ValidateDispatch(
            session2, task2, [session1, session2], [task1, task2], [], DefaultOptions);

        Assert.NotNull(error);
        Assert.Contains("not accept-merged", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Queue_reports_next_dispatchable_step()
    {
        var chainId = Guid.NewGuid();
        var session1 = BoundedSession(chainId, 1, "s1");
        var session2 = BoundedSession(chainId, 2, "s2");
        var task1 = RoleTask(session1.Id, "Role 1", WorkTaskStatus.Complete);
        var task2 = RoleTask(session2.Id, "Role 2", WorkTaskStatus.Planned);
        var merged = JsdpTestFixtures.MergedLease(task1.Id, session1.Id);

        var queue = RoleDeliveryChainGate.BuildQueue(
            chainId,
            "/tmp/tinyquest",
            [session1, session2],
            [task1, task2],
            [merged],
            DefaultOptions);

        Assert.Equal(RoleDeliveryChainGate.ModelName, queue.Model);
        Assert.Equal(session2.Id, queue.NextSessionId);
        Assert.Equal(task2.Id, queue.NextTaskId);
        Assert.Equal(1, queue.CompletedRoles);
    }

    private static OperatorSession BoundedSession(Guid chainId, int sequence, string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            WorkspaceRoot = "/tmp/tinyquest",
            ExecutionMode = SessionExecutionMode.BoundedRole,
            DeliveryChainId = chainId,
            DeliverySequence = sequence,
        };

    private static WorkTask RoleTask(Guid sessionId, string title, WorkTaskStatus status) =>
        JsdpTestFixtures.RoleTask(sessionId, title, status);
}

[Trait(TestCategories.Key, TestCategories.Unit)]
public class JsdpProtocolTests
{
    [Fact]
    public void Default_eight_role_chain_matches_jsdp()
    {
        var roles = RoleDeliveryChainTemplates.DefaultEightRoles;
        Assert.Equal(8, roles.Count);
        Assert.Contains("Product Lock", roles[0].Title, StringComparison.Ordinal);
        Assert.Contains("Release Seal", roles[7].Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Executor_handoff_includes_jsdp()
    {
        Assert.Contains("JSDP", JsdpProtocol.ExecutorHandoffSection, StringComparison.Ordinal);
        Assert.Contains("Goal", JsdpProtocol.ExecutorHandoffSection, StringComparison.Ordinal);
    }
}

[Trait(TestCategories.Key, TestCategories.Unit)]
public class WorktreeSeederTests
{
    [Fact]
    public void Skips_git_and_worktree_mirror_paths()
    {
        Assert.True(WorktreeSeeder.ShouldSkipRelativePath(".git/config"));
        Assert.True(WorktreeSeeder.ShouldSkipRelativePath(".joyzoning/worktrees/abc/file.ts"));
        Assert.True(WorktreeSeeder.ShouldSkipRelativePath(".joyzoning/live/task-id/file.ts"));
        Assert.True(WorktreeSeeder.ShouldSkipRelativePath(".joyzoning/agent-manifest.json"));
        Assert.True(WorktreeSeeder.ShouldSkipRelativePath("node_modules/pkg/index.js"));
        Assert.False(WorktreeSeeder.ShouldSkipRelativePath("docs/product-spec.md"));
        Assert.False(WorktreeSeeder.ShouldSkipRelativePath("app/_layout.tsx"));
    }
}
