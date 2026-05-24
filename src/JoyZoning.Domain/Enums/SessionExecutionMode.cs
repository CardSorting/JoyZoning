namespace JoyZoning.Domain.Enums;

/// <summary>How an operator session participates in workspace execution.</summary>
public enum SessionExecutionMode
{
    /// <summary>Legacy single-session t per workspace (may consolidate duplicates).</summary>
    Default = 0,

    /// <summary>One bounded role per session; multiple sessions share a workspace via a delivery chain.</summary>
    BoundedRole = 1,
}
