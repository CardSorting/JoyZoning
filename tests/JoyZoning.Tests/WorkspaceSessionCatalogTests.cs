using JoyZoning.ControlPlane.Services;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class WorkspaceSessionCatalogTests
{
    [Fact]
    public void SelectCanonicalSessions_keeps_one_per_workspace()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "jz-catalog"));
        var sessions = new[]
        {
            new OperatorSession
            {
                Id = Guid.NewGuid(),
                WorkspaceRoot = root,
                UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            },
            new OperatorSession
            {
                Id = Guid.NewGuid(),
                WorkspaceRoot = root + Path.DirectorySeparatorChar,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
            new OperatorSession
            {
                Id = Guid.NewGuid(),
                WorkspaceRoot = Path.Combine(Path.GetTempPath(), "other"),
                UpdatedAt = DateTimeOffset.UtcNow,
            },
        };

        var canonical = WorkspaceSessionCatalog.SelectCanonicalSessions(sessions);
        Assert.Equal(2, canonical.Count);
        Assert.Contains(canonical, s => WorkspacePaths.EqualsNormalized(s.WorkspaceRoot, root));
    }
}
