using JoyZoning.Adapters.Workspace;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class GitWorkspaceStatusTests
{
    [Fact]
    public void ParsePorcelain_maps_modified_and_untracked()
    {
        const string porcelain = """
             M src/foo.cs
            ?? new-file.txt
            """;
        var files = GitWorkspaceStatus.ParsePorcelain(porcelain);
        Assert.Equal(2, files.Count);
        Assert.Contains(files, f => f.Path == "src/foo.cs" && f.ChangeKind == "modified");
        Assert.Contains(files, f => f.Path == "new-file.txt" && f.ChangeKind == "untracked");
    }

    [Fact]
    public void ParsePorcelain_handles_rename_arrow()
    {
        const string porcelain = "R  old.txt -> new.txt";
        var files = GitWorkspaceStatus.ParsePorcelain(porcelain);
        Assert.Single(files);
        Assert.Equal("new.txt", files[0].Path);
        Assert.Equal("renamed", files[0].ChangeKind);
    }

    [Fact]
    public async Task ListChangedFiles_uses_git_in_temp_repo()
    {
        var dir = Path.Combine(Path.GetTempPath(), "jz-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            RunGit(dir, "init");
            RunGit(dir, "config user.email test@test.com");
            RunGit(dir, "config user.name test");
            await File.WriteAllTextAsync(Path.Combine(dir, "tracked.txt"), "v1");
            RunGit(dir, "add tracked.txt");
            RunGit(dir, "commit -m init");

            await File.WriteAllTextAsync(Path.Combine(dir, "tracked.txt"), "v2");
            await File.WriteAllTextAsync(Path.Combine(dir, "new.txt"), "x");

            var files = await GitWorkspaceStatus.ListChangedFilesAsync(dir);
            Assert.Contains(files, f => f.Path == "tracked.txt" && f.ChangeKind == "modified");
            Assert.Contains(files, f => f.Path == "new.txt" && f.ChangeKind == "untracked");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }

    private static void RunGit(string dir, string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("git not available");
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"git {args} failed: {p.StandardError.ReadToEnd()}");
    }
}
