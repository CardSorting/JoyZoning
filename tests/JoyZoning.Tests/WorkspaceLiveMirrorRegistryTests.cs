using JoyZoning.ControlPlane.Services;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class WorkspaceLiveMirrorRegistryTests
{
    [Fact]
    public void Second_lease_cannot_acquire_same_mirror_root()
    {
        var registry = new WorkspaceLiveMirrorRegistry();
        var root = "/tmp/project/.joyzoning/live/" + Guid.NewGuid().ToString("D");
        var leaseA = Guid.NewGuid();
        var leaseB = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        Assert.True(registry.TryAcquire(root, leaseA, taskId, Guid.NewGuid()));
        Assert.False(registry.TryAcquire(root, leaseB, taskId, Guid.NewGuid()));
        Assert.True(registry.HasCollision(root, leaseB));
    }

    [Fact]
    public void Same_lease_can_reacquire_mirror_root()
    {
        var registry = new WorkspaceLiveMirrorRegistry();
        var root = "/tmp/project/.joyzoning/live/mirror";
        var leaseId = Guid.NewGuid();

        Assert.True(registry.TryAcquire(root, leaseId, Guid.NewGuid(), Guid.NewGuid()));
        Assert.True(registry.TryAcquire(root, leaseId, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void Release_allows_new_lease()
    {
        var registry = new WorkspaceLiveMirrorRegistry();
        var root = "/tmp/project/.joyzoning/live/mirror";
        var leaseA = Guid.NewGuid();
        var leaseB = Guid.NewGuid();

        Assert.True(registry.TryAcquire(root, leaseA, Guid.NewGuid(), Guid.NewGuid()));
        registry.Release(root, leaseA);
        Assert.True(registry.TryAcquire(root, leaseB, Guid.NewGuid(), Guid.NewGuid()));
    }
}
