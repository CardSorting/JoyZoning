using System.Net.Http.Json;
using System.Text.Json;

namespace JoyZoning.Cli;

/// <summary>Polls task workspace changes from the control plane (JSDP canonical workspace).</summary>
public static class TaskWatchCommand
{
    public static async Task<int> RunAsync(CliContext ctx, Guid taskId)
    {
        var url = CliArgs.OptStatic(ctx.Args.Raw, "--base-url")
            ?? Environment.GetEnvironmentVariable("JOYZONING_URL")
            ?? "http://127.0.0.1:9470";
        var baseUrl = url.TrimEnd('/');
        var once = ctx.Args.Has("--once");
        var intervalSec = 5;
        if (CliArgs.OptStatic(ctx.Args.Raw, "--interval") is { } intervalRaw
            && int.TryParse(intervalRaw, out var parsed)
            && parsed > 0)
            intervalSec = parsed;

        using var http = new HttpClient { BaseAddress = new Uri(baseUrl + "/") };
        http.DefaultRequestHeaders.Add("Accept", "application/json");

        do
        {
            try
            {
                var response = await http.GetFromJsonAsync<JsonElement>(
                    $"api/tasks/{taskId}/workspace/changed");
                var files = response.TryGetProperty("files", out var f) && f.ValueKind == JsonValueKind.Array
                    ? f.EnumerateArray().Select(e => e.GetString()).Where(s => s is not null).ToList()
                    : new List<string?>();
                var count = files.Count;
                Console.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss}] task {taskId}: {count} changed file(s)");
                foreach (var path in files.Take(12))
                    Console.WriteLine($"  • {path}");
                if (count > 12)
                    Console.WriteLine($"  … and {count - 12} more");
            }
            catch (Exception ex)
            {
                CliOutput.WriteUsageError($"Failed to poll workspace: {ex.Message}");
                return 1;
            }

            if (once)
                break;

            await Task.Delay(TimeSpan.FromSeconds(intervalSec));
        } while (true);

        return 0;
    }
}
