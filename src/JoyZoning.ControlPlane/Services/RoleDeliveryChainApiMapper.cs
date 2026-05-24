using JoyZoning.Domain.Orchestration;

namespace JoyZoning.ControlPlane.Services;

public static class RoleDeliveryChainApiMapper
{
    public static object ToJson(RoleDeliveryChainGate.ChainQueue queue) =>
        new
        {
            model = queue.Model,
            protocol = queue.Jsdp.Protocol,
            chainId = queue.ChainId,
            workspaceRoot = queue.WorkspaceRoot,
            totalRoles = queue.TotalRoles,
            completedRoles = queue.CompletedRoles,
            activeSessionId = queue.ActiveSessionId,
            nextSessionId = queue.NextSessionId,
            nextTaskId = queue.NextTaskId,
            blockReason = queue.BlockReason,
            jsdp = new
            {
                protocol = queue.Jsdp.Protocol,
                currentRoleSequence = queue.Jsdp.CurrentRoleSequence,
                currentRoleTitle = queue.Jsdp.CurrentRoleTitle,
                previousRoleTitle = queue.Jsdp.PreviousRoleTitle,
                previousRoleStatus = queue.Jsdp.PreviousRoleStatus,
                mergeGateStatus = queue.Jsdp.MergeGateStatus,
                nextDispatchEligibility = queue.Jsdp.NextDispatchEligibility,
                nextHumanAction = queue.Jsdp.NextHumanAction,
                blockReason = queue.Jsdp.BlockReason,
            },
            steps = queue.Steps.Select(s => new
            {
                sessionId = s.SessionId,
                sessionName = s.SessionName,
                sequence = s.Sequence,
                taskId = s.TaskId,
                taskTitle = s.TaskTitle,
                role = DeliveryRoleClassifier.RoleLabel(s.Role),
                taskStatus = s.TaskStatus?.ToString(),
                leaseStatus = s.LeaseStatus,
                complete = s.Complete,
                activeLease = s.ActiveLease,
                dispatchable = s.Dispatchable,
                mergeGateStatus = s.MergeGateStatus,
                humanActionRequired = s.HumanActionRequired,
                blockReason = s.BlockReason,
                jsdpComplianceWarnings = s.JsdpComplianceWarnings,
            }),
        };
}
