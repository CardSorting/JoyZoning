namespace JoyZoning.Cli.Tui;

/// <summary>Central slash-command registry (Hermes COMMAND_REGISTRY pattern).</summary>
public static class OperatorSlashRegistry
{
    public sealed record SlashCommandDef(
        string Name,
        string Description,
        string Category,
        params string[] Aliases);

    public static readonly IReadOnlyList<SlashCommandDef> Commands =
    [
        new("help", "Show categorized commands", "Session", "h", "?"),
        new("quit", "Exit the operator TUI", "Exit", "exit", "q"),
        new("doctor", "Control plane + Hermes health", "Info"),
        new("session", "List, use, or create a session", "Session", "sessions"),
        new("tasks", "List tasks for the active session", "Session", "task"),
        new("use", "Set active task id", "Session"),
        new("dispatch", "Dispatch active task (lease)", "Workflow", "run"),
        new("lease", "Show active lease snapshot", "Workflow"),
        new("heartbeat", "Send lease heartbeat", "Workflow", "hb"),
        new("verify", "Run --cmd in worktree and submit report", "Workflow"),
        new("complete", "Human merge (--yes)", "Workflow", "merge"),
        new("workspace", "Git tree/changed/diff (--root or active lease)", "Workflow", "ws"),
        new("events", "Tail recent joy_events", "Info"),
        new("approvals", "List pending approvals", "Info"),
        new("manager", "Send Manager Chat message (streaming)", "Workflow", "plan", "chat"),
        new("watch", "Stream live events (SignalR)", "Info", "stream"),
        new("stop", "Stop watch / poll / manager stream", "Session"),
        new("hermes", "Launch diet-hermes interactive TUI", "Hermes", "tui", "chat"),
        new("json", "Toggle pretty JSON for command output", "Session", "pretty"),
        new("clear", "Clear the screen", "Session", "cls"),
    ];

    public static bool TryResolve(string input, out string canonical, out string args)
    {
        canonical = "";
        args = "";
        var trimmed = input.Trim();
        if (!trimmed.StartsWith('/'))
            return false;

        var body = trimmed[1..];
        var space = body.IndexOf(' ');
        var name = space < 0 ? body : body[..space];
        args = space < 0 ? "" : body[(space + 1)..].Trim();

        foreach (var def in Commands)
        {
            if (def.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                canonical = def.Name;
                return true;
            }

            foreach (var alias in def.Aliases)
            {
                if (alias.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    canonical = def.Name;
                    return true;
                }
            }
        }

        return false;
    }

    public static IReadOnlyList<string> CompletionCandidates(string prefix)
    {
        prefix = prefix.TrimStart('/');
        if (prefix.Length == 0)
            return Commands.Select(c => "/" + c.Name).ToList();

        return Commands
            .SelectMany(c => new[] { c.Name }.Concat(c.Aliases))
            .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(n => "/" + n)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void PrintHelp()
    {
        Console.WriteLine();
        Console.WriteLine("  JoyZoning operator TUI — slash commands");
        Console.WriteLine("  Plain text (no /) sends Manager Chat when a session is active.");
        Console.WriteLine("  Line editing: Tab complete · ↑↓ history · \\ continue · Ctrl+G editor");
        Console.WriteLine("  Ctrl+C interrupts watch/poll; empty Ctrl+C exits.");
        Console.WriteLine();

        foreach (var group in Commands.GroupBy(c => c.Category).OrderBy(g => g.Key))
        {
            Console.WriteLine($"  {group.Key}");
            foreach (var cmd in group.OrderBy(c => c.Name))
            {
                var aliases = cmd.Aliases.Length > 0
                    ? $"  (/{string.Join(", /", cmd.Aliases)})"
                    : "";
                Console.WriteLine($"    /{cmd.Name,-12} {cmd.Description}{aliases}");
            }

            Console.WriteLine();
        }
    }
}
