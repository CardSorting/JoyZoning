namespace JoyZoning.Domain.Orchestration;

public sealed class CommandRunSummary
{
    public string Command { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string Summary { get; init; } = string.Empty;
}

/// <summary>Verification outcome attached to a card lease (preserved on revoke).</summary>
public sealed class VerificationReport
{
    public Guid CardId { get; init; }
    public Guid SessionId { get; init; }
    public IReadOnlyList<string> ChangedFiles { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CommandRunSummary> CommandsRun { get; init; } = Array.Empty<CommandRunSummary>();
    public IReadOnlyList<string> Risks { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> UnresolvedQuestions { get; init; } = Array.Empty<string>();
    public bool ReadyForHumanReview { get; init; }
    public bool AllCommandsPassed => CommandsRun.Count == 0 || CommandsRun.All(c => c.Passed);
}
