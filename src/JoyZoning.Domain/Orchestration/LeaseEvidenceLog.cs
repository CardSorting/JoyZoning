using System.Text.Json;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

public sealed record LeaseEvidenceEntry(
    DateTimeOffset At,
    string Kind,
    StatusChangeActor Actor,
    string? PreviousStatus,
    string? NextStatus,
    string? Reason,
    string? Summary,
    object? Detail);

public static class LeaseEvidenceLog
{
    public static void Append(
        string? evidenceLogJson,
        Action<string> setEvidenceLogJson,
        string kind,
        StatusChangeActor actor,
        ExecutionLeaseStatus? previous,
        ExecutionLeaseStatus? next,
        string? reason = null,
        string? summary = null,
        object? detail = null)
    {
        var entries = Deserialize(evidenceLogJson);
        entries.Add(new LeaseEvidenceEntry(
            DateTimeOffset.UtcNow,
            kind,
            actor,
            previous?.ToString(),
            next?.ToString(),
            reason,
            summary,
            detail));

        setEvidenceLogJson(Serialize(entries));
    }

    public static IReadOnlyList<LeaseEvidenceEntry> DeserializeEntries(string? json) =>
        Deserialize(json);

    private static List<LeaseEvidenceEntry> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return new List<LeaseEvidenceEntry>();

        return JsonSerializer.Deserialize<List<LeaseEvidenceEntry>>(json) ?? new List<LeaseEvidenceEntry>();
    }

    private static string Serialize(List<LeaseEvidenceEntry> entries) =>
        JsonSerializer.Serialize(entries);
}
