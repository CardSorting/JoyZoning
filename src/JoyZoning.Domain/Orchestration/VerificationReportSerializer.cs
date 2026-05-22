using System.Text.Json;

namespace JoyZoning.Domain.Orchestration;

public static class VerificationReportSerializer
{
    public static string Serialize(VerificationReport report) =>
        JsonSerializer.Serialize(report);

    public static VerificationReport Deserialize(string json) =>
        JsonSerializer.Deserialize<VerificationReport>(json)
        ?? throw new InvalidOperationException("Invalid verification report JSON.");
}
