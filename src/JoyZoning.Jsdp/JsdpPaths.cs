namespace JoyZoning.Jsdp;

public static class JsdpPaths
{
    public const string RootDir = ".jsdp";
    public const string RunFile = "run.json";
    public const string ProjectSpecFile = "project-spec.json";
    public const string ConfigFile = "config.json";
    public const string TreeFile = "tree.json";
    public const string LedgerFile = "ledger.jsonl";
    public const string PromptsDir = "prompts";
    public const string ReportsDir = "reports";
    public const string StateDir = "state";
    public const string PlanningContextFile = "planning-context.json";
    public const string PlanSchemaFile = "plan-schema.json";
    public const string PlanningPromptFileName = "planning-external.md";

    public static string Root(string workspaceRoot) => Path.Combine(workspaceRoot, RootDir);

    public static string Run(string workspaceRoot) => Path.Combine(Root(workspaceRoot), RunFile);

    public static string ProjectSpec(string workspaceRoot) => Path.Combine(Root(workspaceRoot), ProjectSpecFile);

    public static string Config(string workspaceRoot) => Path.Combine(Root(workspaceRoot), ConfigFile);

    public static string Tree(string workspaceRoot) => Path.Combine(Root(workspaceRoot), TreeFile);

    public static string Ledger(string workspaceRoot) => Path.Combine(Root(workspaceRoot), LedgerFile);

    public static string Prompts(string workspaceRoot) => Path.Combine(Root(workspaceRoot), PromptsDir);

    public static string Reports(string workspaceRoot) => Path.Combine(Root(workspaceRoot), ReportsDir);

    public static string State(string workspaceRoot) => Path.Combine(Root(workspaceRoot), StateDir);

    public static string PromptFile(string workspaceRoot, string nodeId) =>
        Path.Combine(Prompts(workspaceRoot), $"{nodeId}.md");

    public static string VerificationReportFile(string workspaceRoot, string nodeId) =>
        Path.Combine(Reports(workspaceRoot), $"{nodeId}-verification.md");

    public static string PlanningContext(string workspaceRoot) =>
        Path.Combine(State(workspaceRoot), PlanningContextFile);

    public static string PlanSchema(string workspaceRoot) =>
        Path.Combine(State(workspaceRoot), PlanSchemaFile);

    public static string PlanningPromptFile(string workspaceRoot) =>
        Path.Combine(Prompts(workspaceRoot), PlanningPromptFileName);
}
