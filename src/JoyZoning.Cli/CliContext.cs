namespace JoyZoning.Cli;

public sealed class CliContext
{
    public CliArgs Args { get; init; } = CliArgs.Parse([]);

    public string BaseUrl => Args.BaseUrl;
    public bool PrettyJson => Args.PrettyJson;
    public bool Quiet => Args.Quiet;

    public const string DefaultBaseUrl = "http://127.0.0.1:9470";
    public const string BaseUrlEnv = "JOYZONING_URL";
    public const string SessionEnv = "JOYZONING_SESSION_ID";

    public static CliContext FromArgs(string[] argv) => new() { Args = CliArgs.Parse(argv) };
}

public sealed class CliUsageException : Exception
{
    public CliUsageException(string message) : base(message) { }
}
