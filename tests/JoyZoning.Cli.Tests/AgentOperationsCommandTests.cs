using System.Text.Json;
using JoyZoning.Cli;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Cli.Tests;

public sealed class AgentOperationsCommandTests
{
    [Fact]
    public void Manifest_static_core_matches_domain_source()
    {
        var json = System.Text.Json.JsonSerializer.SerializeToElement(
            AgentOperationsManifest.BuildStatic(),
            JoyZoningCliClient.JsonOptions);

        Assert.Equal("1", json.GetProperty("manifestVersion").GetString());
        Assert.True(json.GetProperty("endpointSummary").GetProperty("total").GetInt32() >= 60);
        Assert.Contains("AGENTS.md", AgentOperationsManifest.ImportantFiles);
    }

    [Fact]
    public async Task Endpoints_agent_safe_filter_returns_subset()
    {
        var json = await CaptureStdoutAsync(() =>
            AgentOperationsCommand.DispatchAsync(
                new JoyZoningCliClient(CliContext.DefaultBaseUrl),
                CliContext.FromArgs(["endpoints", "--json", "--agent-safe"]),
                ["endpoints", "--json", "--agent-safe"]));

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("agentSafeOnly").GetBoolean());
        var endpoints = doc.RootElement.GetProperty("endpoints");
        Assert.True(endpoints.GetArrayLength() > 0);
        Assert.True(endpoints.GetArrayLength() < JoyZoningEndpointRegistry.Endpoints.Count);
        foreach (var endpoint in endpoints.EnumerateArray())
            Assert.True(endpoint.GetProperty("agentSafe").GetBoolean());
    }

    [Fact]
    public async Task Agent_manifest_command_returns_valid_json()
    {
        var json = await CaptureStdoutAsync(() =>
            AgentOperationsCommand.DispatchAsync(
                new JoyZoningCliClient(CliContext.DefaultBaseUrl),
                CliContext.FromArgs(["agent-manifest", "--json"]),
                ["agent-manifest", "--json"]));

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("joyzoning", doc.RootElement.GetProperty("app").GetString());
        Assert.Equal("1", doc.RootElement.GetProperty("manifestVersion").GetString());
        Assert.True(doc.RootElement.TryGetProperty("verification", out var verification));
        Assert.True(doc.RootElement.TryGetProperty("endpointSummary", out var summary));
        Assert.True(summary.GetProperty("syncedWithApi").GetBoolean());
        Assert.True(verification.TryGetProperty("typecheck", out _));
        Assert.True(doc.RootElement.TryGetProperty("protectedPaths", out var paths));
        Assert.Contains(".next/", paths.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Agent_context_command_returns_valid_json()
    {
        var json = await CaptureStdoutAsync(() =>
            AgentOperationsCommand.DispatchAsync(
                new JoyZoningCliClient(CliContext.DefaultBaseUrl),
                CliContext.FromArgs(["agent-context", "--json"]),
                ["agent-context", "--json"]));

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("joyzoning", doc.RootElement.GetProperty("app").GetString());
        Assert.Equal("1", doc.RootElement.GetProperty("manifestVersion").GetString());
        Assert.True(doc.RootElement.TryGetProperty("git", out _));
        Assert.True(doc.RootElement.TryGetProperty("endpointSummary", out _));
        Assert.True(doc.RootElement.TryGetProperty("importantFiles", out _));
        Assert.True(doc.RootElement.TryGetProperty("nextCommands", out _));
    }

    [Fact]
    public async Task Snapshot_command_returns_valid_json()
    {
        var json = await CaptureStdoutAsync(() =>
            AgentOperationsCommand.DispatchAsync(
                new JoyZoningCliClient(CliContext.DefaultBaseUrl),
                CliContext.FromArgs(["snapshot", "--json"]),
                ["snapshot", "--json"]));

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("git", out _));
        Assert.True(doc.RootElement.TryGetProperty("tests", out _));
        Assert.True(doc.RootElement.TryGetProperty("sessions", out _));
    }

    [Fact]
    public async Task Doctor_command_returns_valid_json()
    {
        var ctx = CliContext.FromArgs(["doctor", "--json"]);
        var previous = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var code = await DoctorCommand.RunAsync(ctx);
            using var doc = JsonDocument.Parse(writer.ToString());
            Assert.True(doc.RootElement.TryGetProperty("checks", out var checks));
            Assert.Equal(JsonValueKind.Array, checks.ValueKind);
            Assert.Contains(checks.EnumerateArray(), c => c.GetProperty("id").GetString() == "endpoint_registry");
            Assert.Contains(checks.EnumerateArray(), c => c.GetProperty("id").GetString() == "endpoint_registry_sync");
            Assert.True(code is 0 or 1);
        }
        finally
        {
            Console.SetOut(previous);
        }
    }

    [Fact]
    public async Task Endpoints_command_returns_valid_json()
    {
        var json = await CaptureStdoutAsync(() =>
            AgentOperationsCommand.DispatchAsync(
                new JoyZoningCliClient(CliContext.DefaultBaseUrl),
                CliContext.FromArgs(["endpoints", "--json"]),
                ["endpoints", "--json"]));

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("endpoints", out var endpoints));
        Assert.Equal(JsonValueKind.Array, endpoints.ValueKind);
    }

    [Fact]
    public async Task Endpoints_markdown_command_writes_table()
    {
        var previous = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var code = await AgentOperationsCommand.DispatchAsync(
                new JoyZoningCliClient(CliContext.DefaultBaseUrl),
                CliContext.FromArgs(["endpoints", "--markdown"]),
                ["endpoints", "--markdown"]);
            Assert.Equal(0, code);
            var output = writer.ToString();
            Assert.Contains("# JoyZoning Endpoint Map", output);
            Assert.Contains("create-session", output);
            Assert.Contains("/api/sessions", output);
        }
        finally
        {
            Console.SetOut(previous);
        }
    }

    [Fact]
    public async Task Inspect_command_returns_valid_json()
    {
        var json = await CaptureStdoutAsync(() =>
            AgentOperationsCommand.DispatchAsync(
                new JoyZoningCliClient(CliContext.DefaultBaseUrl),
                CliContext.FromArgs(["inspect", "--json"]),
                ["inspect", "--json"]));

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("JoyZoning", doc.RootElement.GetProperty("project").GetString());
        Assert.True(doc.RootElement.GetProperty("importantFiles").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Manifest_includes_verification_commands()
    {
        var manifest = await AgentOperationsCommand.BuildManifestAsync(
            CliContext.FromArgs(["agent-manifest", "--json"]));
        var json = JsonSerializer.SerializeToElement(manifest, JoyZoningCliClient.JsonOptions);

        var verification = json.GetProperty("verification");
        Assert.Equal("dotnet build JoyZoning.sln --no-restore", verification.GetProperty("typecheck").GetString());
        Assert.Equal("./scripts/run-tests.sh fast", verification.GetProperty("tests").GetString());
        Assert.Equal("dotnet build JoyZoning.sln", verification.GetProperty("build").GetString());
    }

    [Fact]
    public void Endpoint_registry_includes_session_endpoints()
    {
        Assert.Contains(JoyZoningEndpointRegistry.Endpoints,
            e => e.Id == "create-session" && e.Method == "POST" && e.Path == "/api/sessions");
        Assert.Contains(JoyZoningEndpointRegistry.Endpoints,
            e => e.Id == "list-sessions" && e.Method == "GET" && e.Path == "/api/sessions");
    }

    [Fact]
    public void Protected_paths_are_present()
    {
        Assert.Contains(".next/", AgentOperationsCommand.ProtectedPaths);
        Assert.Contains("node_modules/", AgentOperationsCommand.ProtectedPaths);
        Assert.Contains("generated/", AgentOperationsCommand.ProtectedPaths);
    }

    [Fact]
    public void Doctor_fails_gracefully_when_required_assumptions_are_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), "jz-agent-doctor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var report = AgentOperationsCommand.BuildLocalDoctor(root);

            Assert.False(report.Ok);
            Assert.Contains(report.Checks, c => c.Id == "workspace_root" && c.Status == "fail");
            Assert.Contains(report.Checks, c => c.Id == "required_files" && c.Status == "fail");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static async Task<string> CaptureStdoutAsync(Func<Task<int>> action)
    {
        var previous = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var code = await action();
            Assert.Equal(0, code);
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(previous);
        }
    }
}
