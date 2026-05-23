using System.Text.Json;

namespace JoyZoning.Cli;

public static class CliOutput
{
    public static int WriteResult(CliContext ctx, CliHttpResult result)
    {
        if (!result.IsSuccess)
        {
            WriteError(ctx, result);
            return result.IsNetworkError ? 2 : 1;
        }

        if (ctx.Quiet)
            return 0;

        if (result.Body is null || result.Body.Value.ValueKind == JsonValueKind.Undefined)
        {
            if (!string.IsNullOrWhiteSpace(result.RawText))
                WriteStdout(ctx, result.RawText);
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(ctx.Args.FieldPath))
        {
            var extracted = CliJson.ExtractField(result.Body.Value, ctx.Args.FieldPath);
            if (extracted is not null)
                Console.Out.WriteLine(extracted);
            return 0;
        }

        WriteStdout(ctx, result.Body.Value);
        return 0;
    }

    public static int WriteEnvelope(CliContext ctx, object envelope)
    {
        if (ctx.Quiet)
            return 0;

        var json = JsonSerializer.SerializeToElement(envelope, JoyZoningCliClient.JsonOptions);
        if (!string.IsNullOrWhiteSpace(ctx.Args.FieldPath))
        {
            var extracted = CliJson.ExtractField(json, ctx.Args.FieldPath);
            if (extracted is not null)
                Console.Out.WriteLine(extracted);
            return 0;
        }

        WriteStdout(ctx, json);
        return 0;
    }

    public static void WriteError(CliContext ctx, CliHttpResult result)
    {
        var errObj = new Dictionary<string, object?>
        {
            ["ok"] = false,
            ["status"] = (int)result.StatusCode,
            ["error"] = result.Error ?? (result.IsNetworkError ? "network_error" : "request_failed"),
            ["message"] = result.Message ?? result.RawText,
        };
        var el = JsonSerializer.SerializeToElement(errObj, JoyZoningCliClient.JsonOptions);
        WriteStderr(ctx, el);
    }

    public static void WriteUsageError(string message)
    {
        var el = JsonSerializer.SerializeToElement(new { ok = false, error = "usage_error", message },
            JoyZoningCliClient.JsonOptions);
        Console.Error.WriteLine(JsonSerializer.Serialize(el, JoyZoningCliClient.JsonOptions));
    }

    private static void WriteStdout(CliContext ctx, JsonElement element)
    {
        var options = JoyZoningCliClient.JsonOptions;
        if (ctx.PrettyJson)
            options = new JsonSerializerOptions(options) { WriteIndented = true };
        Console.Out.WriteLine(JsonSerializer.Serialize(element, options));
    }

    private static void WriteStdout(CliContext ctx, string text) => Console.Out.WriteLine(text);

    private static void WriteStderr(CliContext ctx, JsonElement element)
    {
        var options = JoyZoningCliClient.JsonOptions;
        if (ctx.PrettyJson)
            options = new JsonSerializerOptions(options) { WriteIndented = true };
        Console.Error.WriteLine(JsonSerializer.Serialize(element, options));
    }

    public static void PrintHelp() =>
        Console.Out.Write(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "help.txt")));

    public static void PrintHelpEmbedded()
    {
        Console.Out.WriteLine("""
            jz — JoyZoning operator CLI (local kanban runtime)

            Positioning: human-supervised operator shell for the control plane (not a generic curl wrapper).

            Interactive operator TUI (Hermes-style terminal cockpit):
              jz                     Launch when stdin is a TTY (same as jz tui)
              jz tui                 Slash commands, history, live tool stream, Manager Chat
              jz --tui               Alias for jz tui
              jz hermes tui          Full diet-hermes --tui (agent chat; do not rebuild in jz)
              JOYZONING_NO_TUI=1     Disable auto-launch on bare jz

            Global flags:
              --base-url <url>       JOYZONING_URL (default http://127.0.0.1:9470)
              --session <guid>       JOYZONING_SESSION_ID
              --task <guid>          JOYZONING_TASK_ID
              --pretty, -p           Human-readable JSON
              --quiet, -q            Exit code only; errors on stderr
              --yes, -y              Confirm dangerous operations
              --field <path>         Print one field (jq-friendly: .id, .status)
              --tui, -t              Interactive operator shell

            Operator workflows:
              task run <id>          Dispatch + show lease [--poll 5] [--timeout 600]
              task watch <id>        Live build tracker (plain-language progress) [--workspace path]
              task verify <id>       Run --cmd locally in worktree, submit report
              task complete <id>     Human merge (--yes required)
              task fail <id>         Record failure evidence (--reason required)
              task recover <id>      --mode reopen|reattach|replace [--approve-critical]

            Context-aware (omit <id> when cwd is a lease worktree or one active lease):
              lease | heartbeat | verify --cmd "..."

            Diagnostics:
              doctor                 SDK, control plane, optional Hermes
              broccoliq status       BroccoliQ hive + joy-bridge health
              broccoliq audit        Recent mirrored events from hive
              config explain         Env vars and authority rules

            Low-level API (escape hatch): raw <METHOD> <path> [--body @file.json|@stdin]

            Agent harness (DietCode worker — cannot merge/revoke/complete):
              agent start --task <id>
              agent heartbeat
              agent verify --cmd "dotnet build" --cmd "dotnet test"
              agent blocked --reason "..."
              agent done

            YOLO mode (supervised autopilot — not autonomous authority):
              yolo plan --policy .joyzoning/yolo.policy.json   Dry-run task selection (no mutations)
              yolo run --policy <file> --yes                   Autonomous pickup within policy envelope
              yolo stop                                        Request graceful stop
              yolo status                                      Show run state (~/.joyzoning/yolo-run.state.json)

            Humans still own merge and Complete. See docs/yolo-mode.md.

            Reads .joyzoning/context.json from the lease worktree automatically.

            Run: jz doctor   Docs: docs/cli.md
            """);
    }
}
