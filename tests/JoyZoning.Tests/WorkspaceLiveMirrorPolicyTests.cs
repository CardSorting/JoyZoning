using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class WorkspaceLiveMirrorPolicyTests
{
    [Fact]
    public void Shared_session_root_allowed_for_single_active_lease()
    {
        Assert.True(WorkspaceLiveMirrorPolicy.UseSharedSessionRootMirror(
            mirrorEnabled: true,
            LiveMirrorMode.SharedSessionRoot,
            disableSharedWhenParallel: true,
            activeLeasesInWorkspace: 1));
    }

    [Fact]
    public void Shared_session_root_skipped_when_parallel_and_disabled()
    {
        Assert.False(WorkspaceLiveMirrorPolicy.UseSharedSessionRootMirror(
            mirrorEnabled: true,
            LiveMirrorMode.SharedSessionRoot,
            disableSharedWhenParallel: true,
            activeLeasesInWorkspace: 2));

        Assert.True(WorkspaceLiveMirrorPolicy.UseIsolatedMirror(
            mirrorEnabled: true,
            LiveMirrorMode.SharedSessionRoot,
            disableSharedWhenParallel: true,
            activeLeasesInWorkspace: 2));
    }

    [Fact]
    public void PerExecution_uses_isolated_mirror()
    {
        Assert.False(WorkspaceLiveMirrorPolicy.UseSharedSessionRootMirror(
            mirrorEnabled: true,
            LiveMirrorMode.PerExecution,
            disableSharedWhenParallel: true,
            activeLeasesInWorkspace: 1));

        Assert.True(WorkspaceLiveMirrorPolicy.UseIsolatedMirror(
            mirrorEnabled: true,
            LiveMirrorMode.PerExecution,
            disableSharedWhenParallel: true,
            activeLeasesInWorkspace: 1));
    }
}
