using System.Text.Json;

namespace JoyZoning.Cli;

public sealed class YoloRunState
{
    public Guid RunId { get; set; }
    public Guid SessionId { get; set; }
    public string PolicyPath { get; set; } = "";
    public string? AuditLogPath { get; set; }
    public string Phase { get; set; } = "idle";
    public bool StopRequested { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? StoppedAt { get; set; }
    public Guid? CurrentTaskId { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksSkipped { get; set; }
    public string? LastError { get; set; }

    public static string StateDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".joyzoning");

    public static string StateFilePath => Path.Combine(StateDirectory, "yolo-run.state.json");

    public static YoloRunState? TryLoad()
    {
        if (!File.Exists(StateFilePath))
            return null;

        try
        {
            var json = File.ReadAllText(StateFilePath);
            return JsonSerializer.Deserialize<YoloRunState>(json, JoyZoningCliClient.JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(StateDirectory);
        var json = JsonSerializer.Serialize(this, JoyZoningCliClient.JsonOptions);
        File.WriteAllText(StateFilePath, json);
    }

    public static void Clear() =>
        File.Delete(StateFilePath);
}
