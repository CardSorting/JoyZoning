using System.Net;
using System.Text.Json;
using JoyZoning.Cli;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Cli.Tests;

public class YoloModeTests
{
    public YoloModeTests()
    {
        try { YoloRunState.Clear(); } catch { /* ignore */ }
    }

    [Fact]
    public async Task Plan_does_not_call_dispatch_merge_or_complete()
    {
        var client = new RecordingYoloClient();
        var policy = SamplePolicy();
        policy.AllowedTaskTags = [];
        policy.ForbiddenTaskTags = [];
        client.Tasks.Add(TaskJson(Guid.NewGuid(), "Plan only", RiskLevel.Low, WorkTaskStatus.Backlog, "tags: docs"));

        var result = await YoloRunner.PlanAsync(client, policy);
        var json = JsonSerializer.Serialize(result);

        Assert.DoesNotContain("dispatch", client.Calls, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Merge", client.Calls);
        Assert.DoesNotContain("Complete", client.Calls);
        Assert.Contains("ListTasks", client.Calls);
        Assert.Contains("GetLease", client.Calls);
        Assert.True(json.Contains("\"dryRun\":true", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Run_skips_critical_without_dispatch()
    {
        var client = new RecordingYoloClient();
        var criticalId = Guid.NewGuid();
        client.Tasks.Add(TaskJson(criticalId, "Critical card", RiskLevel.Critical, WorkTaskStatus.Planned,
            "tags: prod"));

        var policy = SamplePolicy();
        await YoloRunner.RunAsync(client, YesArgs(), policy);

        Assert.DoesNotContain(client.Calls, c => c.StartsWith("Dispatch", StringComparison.Ordinal));
        Assert.Contains(client.Evidence, e => e.Kind == YoloEvidenceKinds.TaskSkipped);
    }

    [Fact]
    public async Task Run_never_calls_merge_or_complete()
    {
        var client = new RecordingYoloClient();
        var taskId = Guid.NewGuid();
        client.Tasks.Add(TaskJson(taskId, "Docs fix #docs", RiskLevel.Low, WorkTaskStatus.Planned, "tags: docs"));
        client.DispatchSucceeds = true;
        client.LeaseAfterDispatch = LeaseJson(taskId, CreateWorktree());
        client.VerificationAlwaysPasses = true;
        client.DoneSetsReadyForReview = true;

        var policy = SamplePolicy();
        policy.RequiredVerificationCommands = ["true"];
        policy.RequireCleanWorktreeBeforeStart = false;

        await YoloRunner.RunAsync(client, YesArgs(), policy);

        Assert.DoesNotContain(client.Calls, c => c.Contains("Merge", StringComparison.Ordinal));
        Assert.DoesNotContain(client.Calls, c => c.Contains("Complete", StringComparison.Ordinal));
        Assert.DoesNotContain(client.Calls, c => c.Contains("Raw", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Run_respects_max_risk_level_medium()
    {
        var client = new RecordingYoloClient();
        var highId = Guid.NewGuid();
        client.Tasks.Add(TaskJson(highId, "High risk", RiskLevel.High, WorkTaskStatus.Backlog, "tags: docs"));

        var policy = SamplePolicy();
        policy.MaxRiskLevel = "medium";

        await YoloRunner.RunAsync(client, YesArgs(), policy);

        Assert.DoesNotContain(client.Calls, c => c.StartsWith("Dispatch", StringComparison.Ordinal));
        Assert.Contains(client.Evidence, e => e.Kind == YoloEvidenceKinds.TaskSkipped);
    }

    [Fact]
    public async Task Run_stops_on_policy_violation_in_forbidden_command()
    {
        var client = new RecordingYoloClient();
        var policy = SamplePolicy();
        policy.RequiredVerificationCommands = ["git push origin main"];

        await Assert.ThrowsAsync<YoloPolicyViolationException>(() =>
            YoloRunner.RunAsync(client, YesArgs(), policy));
    }

    [Fact]
    public async Task Run_blocks_after_verification_retry_exhaustion()
    {
        var client = new RecordingYoloClient();
        var taskId = Guid.NewGuid();
        client.Tasks.Add(TaskJson(taskId, "Test #test", RiskLevel.Low, WorkTaskStatus.Planned, "tags: test"));
        client.DispatchSucceeds = true;
        client.LeaseAfterDispatch = LeaseJson(taskId, CreateWorktree());
        client.VerificationAlwaysPasses = false;

        var policy = SamplePolicy();
        policy.MaxVerificationRetries = 1;
        policy.RequiredVerificationCommands = ["false"];
        policy.RequireCleanWorktreeBeforeStart = false;

        await YoloRunner.RunAsync(client, YesArgs(), policy);

        Assert.Contains(client.Evidence, e => e.Kind == YoloEvidenceKinds.Blocked);
        Assert.Contains(client.Calls, c => c.StartsWith("AgentStatus:Blocked", StringComparison.Ordinal));
        Assert.Contains(client.Evidence, e => e.Kind == YoloEvidenceKinds.VerifyRetry);
    }

    [Fact]
    public async Task Run_produces_ready_for_review_on_passing_verification()
    {
        var client = new RecordingYoloClient();
        var taskId = Guid.NewGuid();
        client.Tasks.Add(TaskJson(taskId, "Docs #docs", RiskLevel.Low, WorkTaskStatus.Planned, "tags: docs"));
        client.DispatchSucceeds = true;
        client.LeaseAfterDispatch = LeaseJson(taskId, CreateWorktree());
        client.VerificationAlwaysPasses = true;
        client.DoneSetsReadyForReview = true;

        var policy = SamplePolicy();
        policy.RequiredVerificationCommands = ["true"];
        policy.RequireCleanWorktreeBeforeStart = false;

        await YoloRunner.RunAsync(client, YesArgs(), policy);

        Assert.Contains(client.Evidence, e => e.Kind == YoloEvidenceKinds.ReadyForReview);
        Assert.Contains(client.Calls, c => c.StartsWith("SubmitVerification", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Run_emits_yolo_evidence_events()
    {
        var client = new RecordingYoloClient();
        var taskId = Guid.NewGuid();
        client.Tasks.Add(TaskJson(taskId, "Docs #docs", RiskLevel.Low, WorkTaskStatus.Planned, "tags: docs"));
        client.DispatchSucceeds = true;
        client.LeaseAfterDispatch = LeaseJson(taskId, CreateWorktree());
        client.VerificationAlwaysPasses = true;
        client.DoneSetsReadyForReview = true;

        var policy = SamplePolicy();
        policy.RequiredVerificationCommands = ["true"];
        policy.RequireCleanWorktreeBeforeStart = false;

        await YoloRunner.RunAsync(client, YesArgs(), policy);

        var state = YoloRunState.TryLoad();
        Assert.NotNull(state?.AuditLogPath);
        Assert.True(File.Exists(state!.AuditLogPath!));
        var auditText = await File.ReadAllTextAsync(state.AuditLogPath!);
        Assert.Contains(YoloEvidenceKinds.Started, auditText);

        var kinds = client.Evidence.Select(e => e.Kind).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(YoloEvidenceKinds.TaskSelected, kinds);
        Assert.Contains(YoloEvidenceKinds.DispatchAttempted, kinds);
        Assert.Contains(YoloEvidenceKinds.ReadyForReview, kinds);
    }

    [Fact]
    public void Yolo_forbidden_calls_rejects_merge()
    {
        Assert.Throws<YoloPolicyViolationException>(() => YoloForbiddenCalls.RejectIfForbidden("merge"));
    }

    [Fact]
    public void Eligibility_requires_allowed_tag_when_configured()
    {
        var policy = SamplePolicy();
        policy.AllowedTaskTags = ["docs"];

        var ok = YoloEligibility.Evaluate(Guid.NewGuid(), "Fix readme #docs", null,
            (int)WorkTaskStatus.Planned, (int)RiskLevel.Low, hasActiveLease: false, null, policy);
        var bad = YoloEligibility.Evaluate(Guid.NewGuid(), "Fix tests #test", null,
            (int)WorkTaskStatus.Planned, (int)RiskLevel.Low, hasActiveLease: false, null, policy);

        Assert.True(ok.IsEligible);
        Assert.False(bad.IsEligible);
    }

    [Fact]
    public async Task Run_recovers_blocked_lease_when_allowRecover()
    {
        var client = new RecordingYoloClient();
        var taskId = Guid.NewGuid();
        client.Tasks.Add(TaskJson(taskId, "Retry #test", RiskLevel.Low, WorkTaskStatus.Blocked, "tags: test"));
        client.DispatchSucceeds = true;
        client.LeaseAfterDispatch = LeaseJson(taskId, CreateWorktree(), ExecutionLeaseStatus.Blocked);
        client.LeaseAfterDispatchRunning = LeaseJson(taskId, CreateWorktree(), ExecutionLeaseStatus.Running);
        client.VerificationAlwaysPasses = true;
        client.DoneSetsReadyForReview = true;

        var policy = SamplePolicy();
        policy.AllowRecover = true;
        policy.RequiredVerificationCommands = ["true"];
        policy.RequireCleanWorktreeBeforeStart = false;
        policy.DispatchSettleTimeoutSeconds = 5;
        policy.DispatchPollIntervalSeconds = 1;

        await YoloRunner.RunAsync(client, YesArgs(), policy);

        Assert.Contains(client.Calls, c => c.StartsWith("Recover:", StringComparison.Ordinal));
        Assert.Contains(client.Calls, c => c.StartsWith("DispatchRetry:", StringComparison.Ordinal));
        Assert.Contains(client.Evidence, e => e.Kind == YoloEvidenceKinds.RecoverAttempted);
    }

    [Fact]
    public void Run_requires_yes_flag_via_cli_safety()
    {
        var args = CliArgs.Parse(["yolo", "run", "--policy", "x.json"]);
        Assert.Throws<CliUsageException>(() =>
            CliSafety.RequireYes(args, "jz yolo run (autonomous supervised execution)"));
    }

    private static YoloPolicy SamplePolicy() => new()
    {
        Enabled = true,
        SessionId = Guid.NewGuid(),
        MaxRiskLevel = "medium",
        AllowedTaskTags = ["docs", "test"],
        MaxTasksPerRun = 5,
        MaxRuntimeMinutes = 60,
        MaxVerificationRetries = 0,
        DispatchSettleTimeoutSeconds = 30,
        DispatchPollIntervalSeconds = 1,
        HeartbeatIntervalSeconds = 5,
        MergeHandoffVerificationCommands = false,
        RequiredVerificationCommands = ["true"],
        RequireHumanMerge = true,
    };

    private static CliArgs YesArgs() => CliArgs.Parse(["yolo", "run", "--policy", "p.json", "--yes"]);

    private static string CreateWorktree()
    {
        var dir = Path.Combine(Path.GetTempPath(), "jz-yolo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, ".joyzoning"));
        return dir;
    }

    private static JsonElement TaskJson(
        Guid id, string title, RiskLevel risk, WorkTaskStatus status, string description)
    {
        var json = $$"""
            {
              "id": "{{id}}",
              "title": "{{title}}",
              "description": "{{description}}",
              "status": {{(int)status}},
              "risk": {{(int)risk}}
            }
            """;
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static JsonElement LeaseJson(
        Guid taskId,
        string worktree,
        ExecutionLeaseStatus status = ExecutionLeaseStatus.Running)
    {
        var sessionId = Guid.NewGuid();
        var handoff = HandoffPacketBuilder.Build(
            new JoyZoning.Domain.Entities.WorkTask
            {
                Id = taskId,
                Title = "test",
                Description = "tags: docs",
                Risk = RiskLevel.Low,
            },
            worktree,
            "joyzoning/test");
        var json = $$"""
            {
              "id": "{{Guid.NewGuid()}}",
              "workTaskId": "{{taskId}}",
              "assignedSessionId": "{{sessionId}}",
              "operatorSessionId": "{{sessionId}}",
              "worktreePath": "{{worktree.Replace("\\", "\\\\")}}",
              "status": {{(int)status}},
              "riskLevel": {{(int)LeaseRiskLevel.Low}},
              "handoffPacketJson": {{JsonSerializer.Serialize(HandoffPacketBuilder.Serialize(handoff))}}
            }
            """;
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private sealed class RecordingYoloClient : IYoloRunClient
    {
        public List<string> Calls { get; } = [];
        public List<(Guid TaskId, string Kind, string Summary)> Evidence { get; } = [];
        public List<JsonElement> Tasks { get; } = [];
        public HashSet<Guid> DispatchedTaskIds { get; } = [];
        public bool DispatchSucceeds { get; set; }
        public JsonElement? LeaseAfterDispatch { get; set; }
        public JsonElement? LeaseAfterDispatchRunning { get; set; }
        public bool VerificationAlwaysPasses { get; set; } = true;
        public bool DoneSetsReadyForReview { get; set; }

        public Task<CliHttpResult> ListTasksAsync(Guid sessionId)
        {
            Calls.Add("ListTasks");
            var arr = Tasks.ToArray();
            var json = JsonSerializer.Serialize(arr);
            return Task.FromResult(CliHttpResult.FromResponse(HttpStatusCode.OK, json));
        }

        public Task<CliHttpResult> GetLeaseAsync(Guid taskId)
        {
            Calls.Add("GetLease");

            if (DispatchedTaskIds.Contains(taskId) &&
                LeaseAfterDispatchRunning is { } running &&
                running.TryGetProperty("workTaskId", out var rwt) &&
                Guid.TryParse(rwt.GetString(), out var rid) &&
                rid == taskId)
                return Task.FromResult(Ok(running));

            if (LeaseAfterDispatch is { } lease &&
                lease.TryGetProperty("workTaskId", out var wt) &&
                Guid.TryParse(wt.GetString(), out var id) &&
                id == taskId)
            {
                var visible = DispatchedTaskIds.Contains(taskId)
                    || (lease.TryGetProperty("status", out var st)
                        && st.GetInt32() == (int)ExecutionLeaseStatus.Blocked);
                if (visible)
                    return Task.FromResult(Ok(lease));
            }

            if (!DispatchedTaskIds.Contains(taskId))
            {
                return Task.FromResult(CliHttpResult.FromResponse(HttpStatusCode.NotFound,
                    """{"error":"lease_not_found"}"""));
            }

            return Task.FromResult(CliHttpResult.FromResponse(HttpStatusCode.NotFound,
                """{"error":"lease_not_found"}"""));
        }

        public Task<CliHttpResult> DispatchTaskAsync(Guid taskId, bool humanApprovedCritical)
        {
            Calls.Add($"Dispatch:{humanApprovedCritical}");
            if (!DispatchSucceeds)
                return Task.FromResult(CliHttpResult.FromResponse(HttpStatusCode.Conflict,
                    """{"error":"dispatch_failed"}"""));
            DispatchedTaskIds.Add(taskId);
            return Task.FromResult(Ok(new { ok = true, taskId }));
        }

        public Task<CliHttpResult> DispatchRetryAsync(
            Guid taskId,
            Guid sessionId,
            StatusChangeActor actor,
            bool humanApprovedCritical)
        {
            Calls.Add($"DispatchRetry:{humanApprovedCritical}");
            DispatchedTaskIds.Add(taskId);
            return Task.FromResult(Ok(new { ok = true, taskId }));
        }

        public Task<CliHttpResult> RecoverLeaseAsync(
            Guid taskId,
            Guid sessionId,
            LeaseRecoveryMode mode,
            StatusChangeActor actor,
            bool humanApprovedCritical)
        {
            Calls.Add($"Recover:{mode}");
            DispatchedTaskIds.Add(taskId);
            return Task.FromResult(Ok(new { ok = true, mode = (int)mode }));
        }

        public Task<CliHttpResult> HeartbeatLeaseAsync(Guid taskId, Guid sessionId, StatusChangeActor actor)
        {
            Calls.Add("Heartbeat");
            return Task.FromResult(Ok(new { ok = true }));
        }

        public Task<CliHttpResult> AgentLeaseStatusAsync(
            Guid taskId, ExecutionLeaseStatus status, string? reason)
        {
            Calls.Add($"AgentStatus:{status}");
            return Task.FromResult(Ok(new { status = (int)status, reason }));
        }

        public Task<CliHttpResult> SubmitVerificationAsync(
            Guid taskId, VerificationReport report, bool supersede)
        {
            Calls.Add("SubmitVerification");
            var ready = report.ReadyForHumanReview && report.CommandsRun.All(c => c.Passed);
            var status = ready && DoneSetsReadyForReview
                ? (int)ExecutionLeaseStatus.ReadyForReview
                : (int)ExecutionLeaseStatus.Verifying;
            return Task.FromResult(Ok(new { status }));
        }

        public Task<CliHttpResult> RecordAgentEvidenceAsync(
            Guid taskId, string kind, string summary, object? detail = null)
        {
            Evidence.Add((taskId, kind, summary));
            Calls.Add($"Evidence:{kind}");
            return Task.FromResult(Ok(new { ok = true }));
        }

        private static CliHttpResult Ok(object body) =>
            Ok(JsonSerializer.SerializeToElement(body));

        private static CliHttpResult Ok(JsonElement body) =>
            CliHttpResult.FromResponse(HttpStatusCode.OK, JsonSerializer.Serialize(body));
    }
}
