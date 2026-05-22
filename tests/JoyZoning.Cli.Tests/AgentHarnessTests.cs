using JoyZoning.Cli;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Cli.Tests;

public class AgentHarnessTests
{
    [Fact]
    public void Agent_mode_rejects_merge_subcommand()
    {
        var args = CliArgs.Parse(["agent", "task", "merge", Guid.NewGuid().ToString()]);
        Assert.True(args.AgentMode);
        Assert.Throws<CliUsageException>(() => AgentGuard.RejectForbiddenSubcommand("merge"));
    }

    [Fact]
    public void Agent_mode_rejects_revoke_and_complete()
    {
        var args = CliArgs.Parse(["--agent", "task", "revoke", Guid.NewGuid().ToString()]);
        Assert.Throws<CliUsageException>(() => AgentGuard.RejectForbiddenSubcommand("revoke"));
        Assert.Throws<CliUsageException>(() => AgentGuard.RejectForbiddenSubcommand("complete"));
    }

    [Fact]
    public void Agent_cannot_set_complete_status()
    {
        Assert.Throws<CliUsageException>(() =>
            AgentGuard.RejectCompleteStatus(WorkTaskStatus.Complete));
    }

    [Fact]
    public void Missing_context_gives_clear_error()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            AgentGuard.RequireRuntimeContext(null));
        Assert.Contains(".joyzoning/context.json", ex.Message);
    }

    [Fact]
    public void Context_file_roundtrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "jz-ctx-" + Guid.NewGuid().ToString("N"));
        var worktree = Path.Combine(dir, "wt");
        Directory.CreateDirectory(worktree);

        try
        {
            var lease = new JoyZoning.Domain.Entities.ExecutionLease
            {
                Id = Guid.NewGuid(),
                WorkTaskId = Guid.NewGuid(),
                AssignedSessionId = Guid.NewGuid(),
                OperatorSessionId = Guid.NewGuid(),
                WorktreePath = worktree,
                Status = ExecutionLeaseStatus.Running,
            };

            JoyZoningRuntimeContext.Write(lease, "http://127.0.0.1:9470",
                ["dotnet test"],
                new LastVerificationState
                {
                    At = DateTimeOffset.UtcNow,
                    AllPassed = true,
                    Commands = ["dotnet test"],
                });

            var loaded = JoyZoningRuntimeContext.TryLoad(worktree);
            Assert.NotNull(loaded);
            Assert.Equal(lease.WorkTaskId, loaded!.TaskId);
            Assert.True(loaded.LastVerification!.AllPassed);
            Assert.Contains("task complete", loaded.ForbiddenActions);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Worktree_guard_rejects_outside_directory()
    {
        var worktree = Path.Combine(Path.GetTempPath(), "jz-wt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(worktree);
        try
        {
            var ctx = new JoyZoningRuntimeContext
            {
                TaskId = Guid.NewGuid(),
                LeaseId = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                WorktreePath = worktree,
            };
            var prev = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(Path.GetTempPath());
                Assert.Throws<CliUsageException>(() => AgentGuard.RequireWorktree(ctx));
            }
            finally
            {
                Directory.SetCurrentDirectory(prev);
            }
        }
        finally
        {
            try { Directory.Delete(worktree, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Done_semantics_documented_in_forbidden_actions()
    {
        Assert.Contains("agent done", JoyZoningRuntimeContext.DefaultAuthorityRules
            .First(r => r.Contains("ready_for_human_review", StringComparison.OrdinalIgnoreCase)));
    }
}
