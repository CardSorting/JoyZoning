using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JoyZoning.Tests.Infrastructure;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Integration)]
[Collection("OrchestrationApi")]
public class WorkspaceApiIntegrationTests : IAsyncLifetime
{
    private readonly JoyZoningApiCollectionFixture _fixture;
    private readonly HttpClient _http;

    public WorkspaceApiIntegrationTests(JoyZoningApiCollectionFixture fixture)
    {
        _fixture = fixture;
        _http = fixture.Client;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Workspace_changed_persists_git_status_event_for_session()
    {
        var root = CreateGitWorkspace();
        try
        {
            var (sessionId, _) = await SeedSessionAsync(root);
            await File.WriteAllTextAsync(Path.Combine(root, "dirty.txt"), "changed");

            var changedResp = await _http.GetAsync(
                $"api/workspace/changed?workspaceRoot={Uri.EscapeDataString(root)}&sessionId={sessionId}");
            Assert.Equal(HttpStatusCode.OK, changedResp.StatusCode);

            var eventsResp = await _http.GetAsync(
                $"api/events?correlationId={sessionId}&types=git.status.changed");
            eventsResp.EnsureSuccessStatusCode();
            var events = await eventsResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Array, events!.ValueKind);
            Assert.True(events.GetArrayLength() >= 1);
            Assert.Contains(events.EnumerateArray(), e =>
                e.GetProperty("type").GetString() == "git.status.changed");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
        }
    }

    private async Task<(Guid SessionId, Guid TaskId)> SeedSessionAsync(string workspaceRoot)
    {
        var api = new OrchestrationApiClient(_http);
        return await api.SeedTaskAsync(workspaceRoot);
    }

    private static string CreateGitWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "jz-ws-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        RunGit(root, "init");
        RunGit(root, "config user.email test@test.com");
        RunGit(root, "config user.name test");
        File.WriteAllText(Path.Combine(root, "README.md"), "init");
        RunGit(root, "add README.md");
        RunGit(root, "commit -m init");
        return root;
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
