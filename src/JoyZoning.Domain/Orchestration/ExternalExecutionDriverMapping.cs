using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

public static class ExternalExecutionDriverMapping
{
    public static bool TryParseAgent(string? agent, out ExecutionDriver driver, out string? displayName)
    {
        driver = ExecutionDriver.ExternalCustom;
        displayName = agent?.Trim();

        if (string.IsNullOrWhiteSpace(agent))
        {
            displayName = null;
            return false;
        }

        switch (agent.Trim().ToLowerInvariant())
        {
            case "cursor":
                driver = ExecutionDriver.ExternalCursor;
                displayName = "Cursor";
                return true;
            case "claude-code":
            case "claude":
                driver = ExecutionDriver.ExternalClaudeCode;
                displayName = "Claude Code";
                return true;
            case "copilot":
                driver = ExecutionDriver.ExternalCopilot;
                displayName = "Copilot";
                return true;
            case "manual":
                driver = ExecutionDriver.ExternalManual;
                displayName = "Manual";
                return true;
            default:
                driver = ExecutionDriver.ExternalCustom;
                displayName = agent.Trim();
                return true;
        }
    }

    public static string DriverLabel(ExecutionDriver driver) => driver switch
    {
        ExecutionDriver.ExternalCursor => "Cursor",
        ExecutionDriver.ExternalClaudeCode => "Claude Code",
        ExecutionDriver.ExternalCopilot => "Copilot",
        ExecutionDriver.ExternalManual => "Manual",
        ExecutionDriver.ExternalCustom => "Custom",
        ExecutionDriver.Hermes => "Hermes",
        _ => driver.ToString(),
    };
}
