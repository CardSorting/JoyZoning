namespace JoyZoning.Domain.Enums;

/// <summary>Who executes work for a task — JoyZoning supervises regardless of driver.</summary>
public enum ExecutionDriver
{
    Hermes = 0,
    ExternalCursor = 1,
    ExternalClaudeCode = 2,
    ExternalCopilot = 3,
    ExternalManual = 4,
    ExternalCustom = 5,
}
