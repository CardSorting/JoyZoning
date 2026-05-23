using JoyZoning.Adapters.Workspace;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class WorkspaceGitMergerTests : IDisposable
{
    private readonly string _root;
    private readonly WorkspaceGitMerger _merger = new();

    public WorkspaceGitMergerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "jz-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    [Fact]
    public async Task Squash_branch_applies_worker_commit_to_main()
    {
        await InitMainAsync();
        var worktree = Path.Combine(_root, ".joyzoning", "worktrees", "abc");
        const string branch = "joyzoning/card-abc";
        await GitCommandRunner.RunAsync(_root, $"checkout -b \"{branch}\"", default);
        Directory.CreateDirectory(worktree);
        await File.WriteAllTextAsync(Path.Combine(worktree, "feature.txt"), "worker");
        await GitCommandRunner.RunAsync(_root, "add .joyzoning/worktrees/abc/feature.txt", default);
        await GitCommandRunner.RunAsync(_root, "commit -m \"worker\"", default);
        await GitCommandRunner.RunAsync(_root, "checkout main", default);

        var result = await _merger.ConvergeAsync(new WorkspaceConvergenceRequest(
            _root,
            worktree,
            branch));

        Assert.True(result.Succeeded, result.ErrorMessage ?? "merge failed");
        Assert.Equal("squash_branch", result.Strategy);
        Assert.NotEqual(result.DestinationPreviousHead, result.DestinationNewHead);
        Assert.True(File.Exists(Path.Combine(worktree, "feature.txt")));
    }

    [Fact]
    public async Task Preflight_does_not_change_head()
    {
        await InitMainAsync();
        var worktree = Path.Combine(_root, ".joyzoning", "worktrees", "abc");
        Directory.CreateDirectory(worktree);
        await File.WriteAllTextAsync(Path.Combine(worktree, "only-worktree.txt"), "x");

        var before = await GitCommandRunner.ReadRevAsync(_root, "HEAD", default);
        var result = await _merger.PreflightAsync(new WorkspaceConvergenceRequest(
            _root,
            worktree,
            "joyzoning/card-missing",
            DryRun: true));
        var after = await GitCommandRunner.ReadRevAsync(_root, "HEAD", default);

        Assert.True(result.Succeeded, result.ErrorMessage ?? "merge failed");
        Assert.Equal(before, after);
    }

    private async Task InitMainAsync()
    {
        await GitCommandRunner.RunAsync(_root, "init", default);
        await GitCommandRunner.RunAsync(_root, "config user.email \"test@joyzoning.local\"", default);
        await GitCommandRunner.RunAsync(_root, "config user.name \"JoyZoning Test\"", default);
        await File.WriteAllTextAsync(Path.Combine(_root, "README.md"), "main");
        await GitCommandRunner.RunAsync(_root, "add README.md", default);
        await GitCommandRunner.RunAsync(_root, "commit -m \"init\"", default);
        await GitCommandRunner.RunAsync(_root, "branch -M main", default);
    }
}
