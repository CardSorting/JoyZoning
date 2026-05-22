using JoyZoning.Domain.Entities;

namespace JoyZoning.Domain.Orchestration;

public static class VerificationReportValidator
{
    public static string? ValidateStructure(VerificationReport report, Guid expectedCardId)
    {
        if (report.CardId != expectedCardId)
            return "Report cardId does not match lease.";

        if (report.SessionId == Guid.Empty)
            return "Report sessionId is required.";

        if (report.CommandsRun.Count == 0)
            return "At least one verification command is required.";

        foreach (var cmd in report.CommandsRun)
        {
            if (string.IsNullOrWhiteSpace(cmd.Command))
                return "Each verification command must have a command name.";
            if (string.IsNullOrWhiteSpace(cmd.Summary))
                return "Each verification command must have a summary.";
        }

        return null;
    }

    public static bool IsPassingReport(VerificationReport report) =>
        report.CommandsRun.Count > 0
        && report.CommandsRun.All(c => c.Passed)
        && report.ReadyForHumanReview;

    public static string? ValidatePassingSubmission(ExecutionLease lease, VerificationReport report)
    {
        var structure = ValidateStructure(report, lease.WorkTaskId);
        if (structure is not null)
            return structure;

        if (lease.Status != Enums.ExecutionLeaseStatus.Verifying)
            return "Verification can only be submitted while lease is verifying.";

        if (!report.AllCommandsPassed)
            return "All verification commands must pass before review.";

        if (!report.ReadyForHumanReview)
            return "Report must mark readyForHumanReview when verification passes.";

        return null;
    }

    public static string? ValidateCanReplaceExistingPassingReport(
        string? existingVerificationJson,
        VerificationReport incoming,
        bool supersede)
    {
        if (string.IsNullOrWhiteSpace(existingVerificationJson))
            return null;

        VerificationReport existing;
        try
        {
            existing = VerificationReportSerializer.Deserialize(existingVerificationJson);
        }
        catch
        {
            return null;
        }

        if (!IsPassingReport(existing))
            return null;

        if (supersede)
            return null;

        if (!IsPassingReport(incoming))
            return "Cannot replace a passing verification report with failed or incomplete data.";

        return "A passing verification report already exists; set supersede=true to replace it.";
    }
}
