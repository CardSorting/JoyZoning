using System.Text.Json;

namespace JoyZoning.Cli;

/// <summary>Append-only run audit at ~/.joyzoning/yolo-runs/{runId}.jsonl (not a placeholder).</summary>
public sealed class YoloAuditLog
{
    private readonly string _path;
    private readonly object _lock = new();

    public YoloAuditLog(Guid runId)
    {
        RunId = runId;
        var dir = Path.Combine(YoloRunState.StateDirectory, "yolo-runs");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, $"{runId:N}.jsonl");
    }

    public Guid RunId { get; }

    public string FilePath => _path;

    public void Append(string kind, string summary, object? detail = null)
    {
        var entry = new
        {
            at = DateTimeOffset.UtcNow,
            runId = RunId,
            kind,
            summary,
            detail,
        };
        var line = JsonSerializer.Serialize(entry, JoyZoningCliClient.JsonOptions);
        lock (_lock)
        {
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }
}
