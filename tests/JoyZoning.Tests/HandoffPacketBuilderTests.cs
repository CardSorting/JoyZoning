using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class HandoffPacketBuilderTests
{
    [Fact]
    public void Dotnet_task_uses_src_paths_and_dotnet_verify()
    {
        var task = new WorkTask
        {
            Title = "Fix CLI",
            Description = "Update JoyZoning.Cli dispatcher",
            Risk = RiskLevel.Low,
        };

        var packet = HandoffPacketBuilder.Build(task, "/tmp/wt", "joyzoning/card-abc");

        Assert.Contains("src/", packet.AllowedPaths);
        Assert.DoesNotContain("app/", packet.AllowedPaths);
        Assert.Equal("dotnet build", packet.VerificationCommands[0]);
    }

    [Fact]
    public void Expo_task_allows_mobile_root_configs_and_npm_verify()
    {
        var task = new WorkTask
        {
            Title = "TinyQuest Campfire — Expo mobile MVP",
            Description = "React Native + Expo Router mobile app",
            Risk = RiskLevel.Low,
        };

        var packet = HandoffPacketBuilder.Build(task, "/tmp/wt", "joyzoning/card-abc");

        Assert.Contains("app/", packet.AllowedPaths);
        Assert.Contains("features/", packet.AllowedPaths);
        Assert.Contains("package.json", packet.AllowedPaths);
        Assert.Equal("npm install", packet.VerificationCommands[0]);
        Assert.Equal("npx tsc --noEmit", packet.VerificationCommands[1]);
    }

    [Fact]
    public void Product_architect_is_docs_only()
    {
        var task = new WorkTask
        {
            Title = "Role 1 — Product Architect",
            Description = "docs/product-spec.md, docs/user-flows.md",
            Risk = RiskLevel.Low,
        };

        var packet = HandoffPacketBuilder.Build(task, "/tmp/wt", "joyzoning/card-abc", "/tmp/session");

        Assert.Equal(DeliveryRoleKind.ProductArchitect, packet.DeliveryRole);
        Assert.Contains("docs/", packet.AllowedPaths);
        Assert.Contains("docs/product-lock.md", packet.AllowedPaths);
        Assert.Contains("app/", packet.ForbiddenPaths);
        Assert.Contains("package.json", packet.ForbiddenPaths);
    }

    [Fact]
    public void Persistence_role_scopes_storage_only()
    {
        var task = new WorkTask
        {
            Title = "Role 6 — Persistence / Reliability Engineer",
            Description = "AsyncStorage layer under shared/storage",
            Risk = RiskLevel.Low,
        };

        var packet = HandoffPacketBuilder.Build(
            task, "/tmp/wt", "joyzoning/card-abc", "/tmp/session",
            new WorktreeSeeder.SeedResult(12, SkippedExistingContent: false));

        Assert.Equal(DeliveryRoleKind.Persistence, packet.DeliveryRole);
        Assert.Contains("shared/storage/", packet.AllowedPaths);
        Assert.Contains("app/", packet.ForbiddenPaths);
        Assert.Equal(12, packet.FoundationFilesSeeded);
    }
}
