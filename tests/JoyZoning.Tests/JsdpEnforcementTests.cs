using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class JsdpEnforcementTests
{
    private static readonly LeaseRuntimeOptions DefaultOptions = new() { MaxActiveLeasesPerSession = 1 };

    [Fact]
    public void Default_eight_roles_have_unique_workspace_keys()
    {
        var chainId = Guid.NewGuid();
        var root = "/tmp/project";
        var keys = Enumerable.Range(1, 8)
            .Select(seq => RoleDeliveryChainKeys.WorkspaceKeyForBoundedRole(root, chainId, seq))
            .ToList();

        Assert.Equal(8, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("Role 1 — Product Lock", DeliveryRoleKind.ProductArchitect)]
    [InlineData("Role 2 — Architecture Lock", DeliveryRoleKind.MobileArchitect)]
    [InlineData("Role 3 — Core Flow", DeliveryRoleKind.QuestDomain)]
    [InlineData("Role 4 — UI Coherence", DeliveryRoleKind.DesignSystem)]
    [InlineData("Role 5 — Data & Persistence", DeliveryRoleKind.Persistence)]
    [InlineData("Role 6 — QA Pass", DeliveryRoleKind.QaAccessibility)]
    [InlineData("Role 7 — Polish & Recovery", DeliveryRoleKind.IntegrationCaptain)]
    [InlineData("Role 8 — Release Seal", DeliveryRoleKind.IntegrationCaptain)]
    public void Classifier_recognizes_jsdp_role_names(string title, DeliveryRoleKind expected)
    {
        var task = new WorkTask { Title = title, Description = title };
        Assert.Equal(expected, DeliveryRoleClassifier.Classify(task));
    }

    [Fact]
    public void EnsureRoleDescription_injects_missing_sections()
    {
        var augmented = JsdpHandoffCompliance.EnsureRoleDescription(
            "Role 1 — Product Lock",
            "Write docs/product-lock.md only.",
            1);

        foreach (var section in JsdpProtocol.RequiredOutputSections)
            Assert.Contains(section, augmented, StringComparison.Ordinal);
    }

    [Fact]
    public void Jsdp_handoff_prompt_includes_protocol_and_required_sections()
    {
        var session = BoundedSession(Guid.NewGuid(), 1);
        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            Title = "Role 1 — Product Lock",
            Description = "docs/product-lock.md",
        };

        var packet = HandoffPacketBuilder.Build(task, "/tmp/ws", "joyzoning/card-test", "/tmp/ws", null, session);
        var prompt = HandoffPacketBuilder.ToExecutorPrompt(packet);

        Assert.Equal("/tmp/ws", packet.WorktreePath);
        Assert.Equal(JsdpProtocol.ProtocolId, packet.Protocol);
        Assert.True(packet.MergeGateRequired);
        Assert.Equal(JsdpProtocol.RequiredOutputSections, packet.RequiredOutputSections);
        Assert.Contains("Goal", prompt, StringComparison.Ordinal);
        Assert.Contains("Follow-Up Notes", prompt, StringComparison.Ordinal);
        Assert.Contains(JsdpProtocol.ExecutorHandoffSection, prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Verification_rejects_jsdp_sessions_missing_handoff_sections()
    {
        var session = BoundedSession(Guid.NewGuid(), 2);
        var task = new WorkTask { Id = Guid.NewGuid(), Title = "Role 2 — Architecture Lock" };
        var report = new VerificationReport
        {
            CommandsRun = [new CommandRunSummary { Command = "dotnet build", Passed = true, Summary = "Build succeeded" }],
            ReadyForHumanReview = true,
        };

        var error = JsdpHandoffCompliance.ValidateVerificationReport(task, session, report);

        Assert.NotNull(error);
        Assert.Contains(JsdpHandoffCompliance.ComplianceErrorCode, error, StringComparison.Ordinal);
        Assert.Contains("Goal", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Verification_accepts_jsdp_sessions_with_all_sections()
    {
        var session = BoundedSession(Guid.NewGuid(), 2);
        var task = new WorkTask { Id = Guid.NewGuid(), Title = "Role 2 — Architecture Lock" };
        var summary = string.Join("\n", JsdpProtocol.RequiredOutputSections);
        var report = new VerificationReport
        {
            CommandsRun = [new CommandRunSummary { Command = "dotnet build", Passed = true, Summary = summary }],
            ReadyForHumanReview = true,
        };

        Assert.Null(JsdpHandoffCompliance.ValidateVerificationReport(task, session, report));
    }

    [Fact]
    public void Second_active_lease_is_blocked_per_session()
    {
        var session = BoundedSession(Guid.NewGuid(), 1);
        var task = RoleTask(session.Id, "Role 1 — Product Lock");
        var lease = new ExecutionLease
        {
            Id = Guid.NewGuid(),
            WorkTaskId = Guid.NewGuid(),
            OperatorSessionId = session.Id,
            Status = ExecutionLeaseStatus.Running,
        };

        var error = BoundedSessionGate.ValidateDispatch(session, task, [task], [lease], DefaultOptions);

        Assert.NotNull(error);
        Assert.Contains("Single-agent session", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Queue_reports_jsdp_block_reason_when_prior_role_incomplete()
    {
        var chainId = Guid.NewGuid();
        var session1 = BoundedSession(chainId, 1);
        var session2 = BoundedSession(chainId, 2);
        var task1 = RoleTask(session1.Id, "Role 1 — Product Lock", WorkTaskStatus.InProgress);
        var task2 = RoleTask(session2.Id, "Role 2 — Architecture Lock");

        var queue = RoleDeliveryChainGate.BuildQueue(
            chainId,
            "/tmp/project",
            [session1, session2],
            [task1, task2],
            [],
            DefaultOptions);

        Assert.Equal("blocked", queue.Jsdp.NextDispatchEligibility);
        Assert.Contains("merge gate closed", queue.Steps[1].BlockReason!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("closed", queue.Jsdp.MergeGateStatus);
    }

    [Fact]
    public void Bounded_role_workspace_keys_prevent_session_consolidation()
    {
        var key = RoleDeliveryChainKeys.WorkspaceKeyForBoundedRole("/tmp/ws", Guid.NewGuid(), 1);
        Assert.True(RoleDeliveryChainKeys.IsBoundedRoleWorkspaceKey(key));
        Assert.NotEqual("/tmp/ws", key);
    }

    [Fact]
    public void Autopilot_blocks_bounded_role_sessions()
    {
        var decision = AuthorityPolicyEvaluator.Evaluate(new AuthorityEvaluationInput(
            AuthorityProfileKind.BalancedAuto,
            AutopilotEnabled: true,
            MetadataOnlyAcceptResult: false,
            ExecutionLeaseStatus.ReadyForReview,
            RiskLevel.Low,
            "ready_to_merge",
            VerificationPassed: true,
            VerificationMissing: false,
            HasConflicts: false,
            OverlappingReadyWorker: false,
            MergeObservabilityUnknown: false,
            DirtyWorktree: false,
            UnknownHeadCommit: false,
            ChangedFilesCount: 1,
            ChangedFiles: ["docs/a.md"],
            LargeChangeSetThreshold: 20,
            IsJsdpEnforcedSession: true));

        Assert.False(decision.AutoAcceptAllowed);
        Assert.Contains(AuthorityReasonCodes.JsdpHumanMergeRequired, decision.ReasonCodes);
    }

    [Fact]
    public void Queue_marks_next_role_eligible_when_prior_converged()
    {
        var chainId = Guid.NewGuid();
        var session1 = BoundedSession(chainId, 1);
        var session2 = BoundedSession(chainId, 2);
        var task1 = RoleTask(session1.Id, "Role 1 — Product Lock", WorkTaskStatus.Complete);
        var task2 = RoleTask(session2.Id, "Role 2 — Architecture Lock");
        var merged = JsdpTestFixtures.MergedLease(task1.Id, session1.Id);

        var queue = RoleDeliveryChainGate.BuildQueue(
            chainId,
            "/tmp/project",
            [session1, session2],
            [task1, task2],
            [merged],
            DefaultOptions);

        Assert.Equal("eligible", queue.Jsdp.NextDispatchEligibility);
        Assert.Null(queue.BlockReason);
        Assert.True(queue.Steps[1].Dispatchable);
        Assert.True(queue.Steps[0].Complete);
    }

    [Fact]
    public void Queue_blocks_when_prior_complete_without_merge()
    {
        var chainId = Guid.NewGuid();
        var session1 = BoundedSession(chainId, 1);
        var session2 = BoundedSession(chainId, 2);
        var task1 = RoleTask(session1.Id, "Role 1 — Product Lock", WorkTaskStatus.Complete);
        var task2 = RoleTask(session2.Id, "Role 2 — Architecture Lock");

        var queue = RoleDeliveryChainGate.BuildQueue(
            chainId,
            "/tmp/project",
            [session1, session2],
            [task1, task2],
            [],
            DefaultOptions);

        Assert.Equal("blocked", queue.Jsdp.NextDispatchEligibility);
        Assert.Contains("not accept-merged", queue.Steps[1].BlockReason!, StringComparison.OrdinalIgnoreCase);
        Assert.False(queue.Steps[0].Complete);
    }

    [Fact]
    public void Downstream_dispatch_requires_lock_artifacts()
    {
        var chainId = Guid.NewGuid();
        var root = Path.Combine(Path.GetTempPath(), "jsdp-lock-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        try
        {
            var session3 = JsdpTestFixtures.BoundedSession(chainId, 3, root);
            var task3 = JsdpTestFixtures.RoleTask(session3.Id, "Role 3 — Core Flow");

            var error = JsdpHandoffCompliance.ValidateLockArtifactsForDispatch(session3, root);
            Assert.NotNull(error);
            Assert.Contains("product-lock.md", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Orphan_bounded_role_requires_integrity()
    {
        var session = new OperatorSession
        {
            Id = Guid.NewGuid(),
            ExecutionMode = SessionExecutionMode.BoundedRole,
        };

        Assert.True(JsdpSessionPolicy.RequiresEnforcement(session));
        Assert.False(JsdpSessionPolicy.IsValidChainMember(session));
        Assert.Contains("jsdp_invalid_session", JsdpSessionPolicy.ValidateSessionIntegrity(session)!);
    }

    [Fact]
    public void Yolo_skips_jsdp_enforced_sessions()
    {
        var candidate = YoloEligibility.Evaluate(
            Guid.NewGuid(),
            "Role 1",
            "desc",
            (int)WorkTaskStatus.Planned,
            (int)RiskLevel.Low,
            hasActiveLease: false,
            activeLeaseStatus: null,
            policy: new YoloPolicy { Enabled = true, SessionId = Guid.NewGuid(), MaxRiskLevel = "low" },
            isJsdpEnforcedSession: true);

        Assert.False(candidate.IsEligible);
        Assert.Contains("delivery-chain", candidate.SkipReason, StringComparison.OrdinalIgnoreCase);
    }

    private static OperatorSession BoundedSession(Guid chainId, int sequence) =>
        JsdpTestFixtures.BoundedSession(chainId, sequence);

    private static WorkTask RoleTask(Guid sessionId, string title, WorkTaskStatus status = WorkTaskStatus.Planned) =>
        JsdpTestFixtures.RoleTask(sessionId, title, status);
}
