namespace JoyZoning.Domain.Orchestration;

public static class RoleDeliveryChainKeys
{
    public static string WorkspaceKeyForBoundedRole(string normalizedWorkspaceRoot, Guid chainId, int sequence) =>
        $"{normalizedWorkspaceRoot}::chain:{chainId:D}:seq:{sequence}";

    public static bool IsBoundedRoleWorkspaceKey(string? workspaceKey) =>
        workspaceKey?.Contains("::chain:", StringComparison.Ordinal) == true;
}
