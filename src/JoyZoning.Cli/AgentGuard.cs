using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Cli;

/// <summary>Hard failures when agent harness rules are violated.</summary>
public static class AgentGuard
{
    public static void RejectHumanOnlyCommand(CliArgs args, string commandDescription)
    {
        if (!args.AgentMode)
            return;

        throw new CliUsageException(
            $"Agent harness cannot run '{commandDescription}'. Use human operator commands (without agent mode) or jz agent * subcommands.");
    }

    public static void RejectForbiddenSubcommand(string subcommand)
    {
        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "complete", "merge", "revoke",
        };

        if (forbidden.Contains(subcommand))
            throw new CliUsageException(
                $"jz task {subcommand} is forbidden in agent harness. " +
                "Use jz agent done (ready for review) or ask a human for merge/revoke.");
    }

    public static void RejectCompleteStatus(WorkTaskStatus status)
    {
        if (status == WorkTaskStatus.Complete)
            throw new CliUsageException(
                "Agents cannot mark tasks Complete. Use jz agent done after verification passes.");
    }

    public static void RequireRuntimeContext(JoyZoningRuntimeContext? ctx)
    {
        if (ctx is null)
            throw new CliUsageException(
                $"Missing {JoyZoningRuntimeContext.RelativePath}. Run jz agent start --task <id> from dispatch, or run inside the lease worktree.");
    }

    public static void RequireWorktree(JoyZoningRuntimeContext ctx, bool strict = true)
    {
        if (!strict)
            return;

        if (!JoyZoningRuntimeContext.IsCurrentDirectoryInsideWorktree(null, ctx.WorktreePath))
            throw new CliUsageException(
                $"Current directory must be inside worktree: {ctx.WorktreePath}");
    }

    public static void RejectCriticalDispatchWithoutApproval(CliArgs args, bool taskIsCritical)
    {
        if (taskIsCritical && !args.ApproveCritical)
            throw new CliUsageException(
                "Critical dispatch from agent context requires human --approve-critical on the operator shell.");
    }
}
