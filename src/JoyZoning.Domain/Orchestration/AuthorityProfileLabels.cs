using JoyZoning.Domain.Configuration;

namespace JoyZoning.Domain.Orchestration;

public static class AuthorityProfileLabels
{
    public static string ToLabel(AuthorityProfileKind profile) =>
        profile switch
        {
            AuthorityProfileKind.Conservative => "Conservative",
            AuthorityProfileKind.BalancedAuto => "Balanced-Auto (bounded YOLO)",
            AuthorityProfileKind.Yolo => "YOLO (bounded)",
            AuthorityProfileKind.Custom => "Custom",
            _ => profile.ToString(),
        };
}
