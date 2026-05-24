using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

/// <summary>Central JSDP session classification — all enforcement surfaces use this.</summary>
public static class JsdpSessionPolicy
{
    public const string InvalidSessionCode = "jsdp_invalid_session";
    public const string EnforcedSessionCode = "jsdp_enforced_session";

    /// <summary>Any bounded-role execution mode requires JSDP gates (including orphan rows).</summary>
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
        "JSDP bounded-role session: use delivery-chain queue and accept-merge between roles, not YOLO.";

    public static string PlanRunSkipReason =>
        "JSDP bounded-role session: use `jz delivery-chain queue` or ./scripts/role-chain-dispatch.sh --next.";
}
