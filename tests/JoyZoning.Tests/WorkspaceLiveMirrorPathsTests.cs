using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class WorkspaceLiveMirrorPathsTests
{
    [Fact]
    public void PerExecution_paths_differ_for_two_executions_on_same_task()
    {
        var session = Path.Combine(Path.GetTempPath(), "jz-mirror-" + Guid.NewGuid().ToString("N"));
        var taskId = Guid.NewGuid();
        var execA = Guid.NewGuid();
        var execB = Guid.NewGuid();

        Assert.True(WorkspaceLiveMirrorPaths.TryResolveMirrorRoot(
            session, taskId, execA, LiveMirrorMode.PerExecution, out var pathA, out _));
        Assert.True(WorkspaceLiveMirrorPaths.TryResolveMirrorRoot(
            session, taskId, execB, LiveMirrorMode.PerExecution, out var pathB, out _));

        Assert.NotEqual(pathA, pathB);
        Assert.Contains(taskId.ToString("D"), pathA, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Two_tasks_resolve_to_different_live_paths()
    {
        var session = Path.Combine(Path.GetTempPath(), "jz-mirror-" + Guid.NewGuid().ToString("N"));
        var taskA = Guid.NewGuid();
        var taskB = Guid.NewGuid();
        var exec = Guid.NewGuid();

        Assert.True(WorkspaceLiveMirrorPaths.TryResolveMirrorRoot(
            session, taskA, exec, LiveMirrorMode.PerExecution, out var pathA, out _));
        Assert.True(WorkspaceLiveMirrorPaths.TryResolveMirrorRoot(
            session, taskB, exec, LiveMirrorMode.PerExecution, out var pathB, out _));

        Assert.NotEqual(pathA, pathB);
    }

    [Fact]
    public void Empty_task_id_is_rejected()
    {
        Assert.False(WorkspaceLiveMirrorPaths.TryResolveMirrorRoot(
            "/tmp/project",
            Guid.Empty,
            Guid.NewGuid(),
            LiveMirrorMode.PerExecution,
            out _,
            out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Resolved_path_stays_under_joyzoning_live()
    {
        var session = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "jz-mirror-" + Guid.NewGuid().ToString("N")));
        var taskId = Guid.NewGuid();
        var execId = Guid.NewGuid();

        Assert.True(WorkspaceLiveMirrorPaths.TryResolveMirrorRoot(
            session, taskId, execId, LiveMirrorMode.PerExecution, out var mirrorRoot, out _));

        var liveBase = Path.GetFullPath(Path.Combine(session, WorkspaceLiveMirrorPaths.LiveRootSegment));
        Assert.StartsWith(liveBase, mirrorRoot, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveMirrorKeyId_uses_execution_session_when_set()
    {
        var lease = new ExecutionLease
        {
            Id = Guid.NewGuid(),
            WorkTaskId = Guid.NewGuid(),
            ExecutionSessionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        };

        var key = WorkspaceLiveMirrorPaths.ResolveMirrorKeyId(lease, LiveMirrorMode.PerExecution);
        Assert.Equal(lease.ExecutionSessionId, key);
    }
}
