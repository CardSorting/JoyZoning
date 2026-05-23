using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JoyZoning.Tests.Infrastructure;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Integration)]
[Collection("OrchestrationApi")]
public class TaskWorkspaceApiIntegrationTests : IAsyncLifetime
{
    private readonly JoyZoningApiCollectionFixture _fixture;
    private readonly OrchestrationApiClient _api;
    private readonly HttpClient _http;

    public TaskWorkspaceApiIntegrationTests(JoyZoningApiCollectionFixture fixture)
    {
        _fixture = fixture;
        _http = fixture.Client;
        _api = new OrchestrationApiClient(_http);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Task_workspace_changed_uses_lease_worktree_after_dispatch()
    {
        var root = CreateGitWorkspace();
        try
        {
            var (_, taskId) = await _api.SeedTaskAsync(root);
            await _api.DispatchAsync(taskId);

            var (_, lease, _, _) = await OrchestrationApiClient.ReadAsync(await _api.GetLeaseAsync(taskId));
            var worktree = lease!.Value.GetProperty("worktreePath").GetString();
            Assert.False(string.IsNullOrWhiteSpace(worktree));

            await File.WriteAllTextAsync(Path.Combine(worktree!, "dirty.txt"), "x");

            var resp = await _http.GetAsync($"api/tasks/{taskId}/workspace/changed");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("worktree", json.GetProperty("inspect").GetString());
            Assert.Equal(worktree, json.GetProperty("workspaceRoot").GetString());
            Assert.Contains(json.GetProperty("files").EnumerateArray(),
                f => f.GetProperty("path").GetString() == "dirty.txt");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
        }
    }

    private static string CreateGitWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "jz-task-ws-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        RunGit(root, "init");
        RunGit(root, "config user.email t@t.com");
        RunGit(root, "config user.name t");
        File.WriteAllText(Path.Combine(root, "README.md"), "v1");
        RunGit(root, "add README.md");
        RunGit(root, "commit -m init");
        return root;
    }

    private static void RunGit(string dir, string args)
    {
        using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = dir,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("git missing");
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new InvalidOperationException(p.StandardError.ReadToEnd());
    }
}
