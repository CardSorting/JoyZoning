namespace JoyZoning.Cli;

/// <summary>Parsed flags and positional arguments for one invocation.</summary>
public sealed class CliArgs
{
    public string[] Raw { get; init; } = [];
    public string[] Positionals { get; init; } = [];
    public string BaseUrl { get; init; } = CliContext.DefaultBaseUrl;
    public Guid? SessionId { get; init; }
    public Guid? TaskId { get; init; }
    public bool PrettyJson { get; init; }
    public bool Quiet { get; init; }
    public bool Yes { get; init; }
    public string? FieldPath { get; init; }

    public const string TaskIdEnv = "JOYZONING_TASK_ID";

    public bool ApproveCritical => Has("--approve-critical") || Has("--critical");

    public bool AgentMode =>
        Has("--agent") ||
        (Positionals.Length > 0 && Positionals[0].Equals("agent", StringComparison.OrdinalIgnoreCase));

    public bool Has(string flag) =>
        Raw.Any(x => x.Equals(flag, StringComparison.Ordinal));

    public string? Opt(string flag)
    {
        for (var i = 0; i < Raw.Length; i++)
        {
            if (Raw[i].StartsWith(flag + "=", StringComparison.Ordinal))
                return Raw[i][(flag.Length + 1)..];
            if (Raw[i] == flag && i + 1 < Raw.Length)
                return Raw[i + 1];
        }

        return null;
    }

    public IReadOnlyList<string> OptAll(string flag)
    {
        var list = new List<string>();
        for (var i = 0; i < Raw.Length; i++)
        {
            if (Raw[i].StartsWith(flag + "=", StringComparison.Ordinal))
                list.Add(Raw[i][(flag.Length + 1)..]);
            else if (Raw[i] == flag && i + 1 < Raw.Length)
                list.Add(Raw[++i]);
        }

        return list;
    }

    public static CliArgs Parse(string[] args)
    {
        var baseUrl = Environment.GetEnvironmentVariable(CliContext.BaseUrlEnv) ?? CliContext.DefaultBaseUrl;
        Guid? session = null;
        Guid? task = null;
        if (Guid.TryParse(Environment.GetEnvironmentVariable(CliContext.SessionEnv), out var sid))
            session = sid;
        if (Guid.TryParse(Environment.GetEnvironmentVariable(TaskIdEnv), out var tid))
            task = tid;

        var pretty = false;
        var quiet = false;
        var yes = false;
        string? field = null;
        var positionals = new List<string>();
        var sawDeprecatedCritical = false;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a is "--pretty" or "-p")
                pretty = true;
            else if (a is "--quiet" or "-q")
                quiet = true;
            else if (a is "--yes" or "-y")
                yes = true;
            else if (a == "--critical")
                sawDeprecatedCritical = true;
            else if (TryParseFlagValue(args, ref i, "--base-url", out var url))
                baseUrl = url!;
            else if (TryParseGuidFlag(args, ref i, "--session", out var gs))
                session = gs;
            else if (TryParseGuidFlag(args, ref i, "--task", out var gt))
                task = gt;
            else if (TryParseFlagValue(args, ref i, "--field", out var fp))
                field = fp;
            else
                positionals.Add(a);
        }

        if (sawDeprecatedCritical)
            Console.Error.WriteLine("warning: --critical is deprecated; use --approve-critical.");

        return new CliArgs
        {
            Raw = args,
            Positionals = positionals.ToArray(),
            BaseUrl = baseUrl,
            SessionId = session,
            TaskId = task,
            PrettyJson = pretty,
            Quiet = quiet,
            Yes = yes,
            FieldPath = field,
        };
    }

    public Guid RequireSessionId(Guid? explicitId)
    {
        var id = explicitId ?? SessionId;
        if (!id.HasValue)
            throw new CliUsageException("Session required: --session <guid> or JOYZONING_SESSION_ID.");
        return id.Value;
    }

    public static Guid? ParseGuidOpt(string[] a, string flag)
    {
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i].StartsWith(flag + "=", StringComparison.Ordinal) &&
                Guid.TryParse(a[i][(flag.Length + 1)..], out var id))
                return id;
            if (a[i] == flag && i + 1 < a.Length && Guid.TryParse(a[i + 1], out var id2))
                return id2;
        }

        return null;
    }

    public static Guid RequireGuid(string[] a, int index, string label)
    {
        if (index >= a.Length || !Guid.TryParse(a[index], out var id))
            throw new CliUsageException($"{label} must be a GUID.");
        return id;
    }

    public static int RequireInt(string[] a, string flag)
    {
        var v = OptStatic(a, flag) ?? throw new CliUsageException($"{flag} is required.");
        if (!int.TryParse(v, out var n))
            throw new CliUsageException($"{flag} must be an integer.");
        return n;
    }

    public static int ParseIntOpt(string[] a, string flag, int defaultValue)
    {
        var v = OptStatic(a, flag);
        return v is null ? defaultValue : int.Parse(v);
    }

    public static string? OptStatic(string[] a, string flag)
    {
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i].StartsWith(flag + "=", StringComparison.Ordinal))
                return a[i][(flag.Length + 1)..];
            if (a[i] == flag && i + 1 < a.Length)
                return a[i + 1];
        }

        return null;
    }

    private static bool TryParseFlagValue(string[] args, ref int i, string flag, out string? value)
    {
        value = null;
        var a = args[i];
        if (a.StartsWith(flag + "=", StringComparison.Ordinal))
        {
            value = a[(flag.Length + 1)..];
            return true;
        }

        if (a == flag && i + 1 < args.Length)
        {
            value = args[++i];
            return true;
        }

        return false;
    }

    private static bool TryParseGuidFlag(string[] args, ref int i, string flag, out Guid guid)
    {
        guid = default;
        if (!TryParseFlagValue(args, ref i, flag, out var v) || v is null)
            return false;
        return Guid.TryParse(v, out guid);
    }
}
