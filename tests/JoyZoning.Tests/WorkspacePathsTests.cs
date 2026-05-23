using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class WorkspacePathsTests
{
    [Fact]
    public void EqualsNormalized_MatchesSamePathWithDifferentSeparators()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "jz-ws-paths"));
        var withSlash = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Assert.True(WorkspacePaths.EqualsNormalized(root, withSlash));
    }

    [Fact]
    public void IsSameOrChildWorkspace_MatchesChildDirectory()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "jz-parent"));
        var child = Path.Combine(root, "sub");
        Assert.True(WorkspacePaths.IsSameOrChildWorkspace(child, root));
    }

    [Fact]
    public void IsSameOrChildWorkspace_RejectsUnrelatedDirectory()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "jz-a"));
        var other = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "jz-b"));
        Assert.False(WorkspacePaths.IsSameOrChildWorkspace(other, root));
    }

    [Fact]
    public void EqualsNormalized_MatchesPrivateUsersAliasOnMacOS()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        Assert.True(WorkspacePaths.EqualsNormalized("/private/Users/test/project", "/Users/test/project"));
    }
}
