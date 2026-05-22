using System.Text.RegularExpressions;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Agents.Approval;

public static class RiskClassifier
{
    private static readonly (Regex Pattern, ApprovalCategory Category, RiskLevel Risk)[] Rules =
    {
        (new Regex(@"git\s+reset\s+--hard", RegexOptions.IgnoreCase), ApprovalCategory.GitDestructive, RiskLevel.Critical),
        (new Regex(@"\brm\s+-rf\b", RegexOptions.IgnoreCase), ApprovalCategory.FileDelete, RiskLevel.Critical),
        (new Regex(@"npm\s+install|pip\s+install|cargo\s+add", RegexOptions.IgnoreCase), ApprovalCategory.PackageInstall, RiskLevel.High),
        (new Regex(@"curl\b|wget\b|fetch\(", RegexOptions.IgnoreCase), ApprovalCategory.Network, RiskLevel.High),
        (new Regex(@"migrate|migration", RegexOptions.IgnoreCase), ApprovalCategory.Migration, RiskLevel.High),
        (new Regex(@"API_KEY|SECRET|PASSWORD|TOKEN", RegexOptions.IgnoreCase), ApprovalCategory.Credential, RiskLevel.Critical),
        (new Regex(@"patch|write_file|remove_file", RegexOptions.IgnoreCase), ApprovalCategory.Patch, RiskLevel.Medium),
    };

    public static (ApprovalCategory Category, RiskLevel Risk) Classify(string command, string? description = null)
    {
        var text = $"{command} {description}";
        foreach (var (pattern, category, risk) in Rules)
        {
            if (pattern.IsMatch(text))
                return (category, risk);
        }

        if (command.Contains("terminal", StringComparison.OrdinalIgnoreCase) ||
            command.Length > 20)
            return (ApprovalCategory.Shell, RiskLevel.Medium);

        return (ApprovalCategory.Other, RiskLevel.Low);
    }
}
