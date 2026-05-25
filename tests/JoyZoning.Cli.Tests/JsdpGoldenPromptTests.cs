using JoyZoning.Jsdp.Services;
using Xunit;

namespace JoyZoning.Cli.Tests;

public sealed class JsdpGoldenPromptTests : IDisposable
{
    private readonly string _root;
    private readonly string _fixture;
    private readonly string _golden;

    public JsdpGoldenPromptTests()
    {
        _fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "jsdp");
        _golden = Path.Combine(_fixture, "golden-001.prompt.md");
        _root = Path.Combine(Path.GetTempPath(), "jsdp-golden-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        File.Copy(Path.Combine(_fixture, "PROJECT_SPEC.md"), Path.Combine(_root, "PROJECT_SPEC.md"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Next_generates_prompt_matching_golden_fixture()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("", Path.Combine(_root, "PROJECT_SPEC.md"));
        harness.Analyze();
        harness.Plan(JoyZoning.Jsdp.Models.JsdpPlanningMode.VerticalSlices);

        var next = harness.Next();
        Assert.True(next.Ready);
        Assert.NotNull(next.PromptPath);

        var actual = File.ReadAllText(next.PromptPath!);
        Assert.Contains("# JSDP Node: 001 — Bootstrap runtime for MiniApp", actual);
        Assert.Contains("echo \"miniapp ok\"", actual);
        Assert.Contains("## Stop Condition", actual);
        Assert.Contains("Do not proceed to future nodes.", actual);
        Assert.Contains("## Required Response Format", actual);

        var golden = File.ReadAllText(_golden);
        foreach (var section in new[] { "## Goal", "## Scope", "## Dependencies", "## Acceptance Criteria", "## Required Verification", "## Allowed Mutation Surface" })
        {
            Assert.Contains(section, actual);
            Assert.Contains(section, golden);
        }
    }
}
