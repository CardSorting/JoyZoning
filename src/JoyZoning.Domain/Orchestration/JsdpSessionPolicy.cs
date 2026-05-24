using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

/// <summary>JoyZoning Sequential Delivery Protocol — default execution model for all delivery.</summary>
public static class JsdpSessionPolicy
{
    public const string InvalidSessionCode = "jsdp_invalid_session";
    public const string EnforcedSessionCode = "jsdp_enforced_session";

    /// <summary>All bounded-role sessions use JSDP gates (including orphan rows).</summary>
    public static bool RequiresEnforcement(OperatorSession? session) =>
        session?.ExecutionMode == SessionExecutionMode.BoundedRole;

    /// <summary>Valid sequential chain member (chain id + sequence present).</summary>
    public static bool IsValidChainMember(OperatorSession? session) =>
        session?.IsBoundedRoleSession == true;

    public static string? ValidateSessionIntegrity(OperatorSession session)
    {
        if (!RequiresEnforcement(session))
            return null;

        if (session.DeliveryChainId.HasValue && session.DeliverySequence is > 0)
            return null;

        return $"{InvalidSessionCode}: BoundedRole session {session.Id} is missing deliveryChainId "
            + "or deliverySequence. Recreate via POST /api/delivery-chains.";
    }

    public static string YoloSkipReason =>
        "Use delivery-chain commands or role-chain-dispatch.sh — YOLO is not the default execution path.";

    public static string PlanRunSkipReason =>
        "Use `jz delivery-chain queue` or ./scripts/role-chain-dispatch.sh --next.";

    /// <summary>External-agent bounded roles satisfy JSDP without a Hermes lease.</summary>
    public static bool IsExternalBoundedExecution(WorkTask? task, OperatorSession? session) =>
        RequiresEnforcement(session) && task?.TaskExecutionMode == TaskExecutionMode.ExternalAgent;

    public static string ExternalExecutionHint =>
        "External JSDP tasks use branch + review + merge gates; Hermes lease is not required.";
}
