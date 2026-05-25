namespace JoyZoning.Jsdp.Models;

public sealed class JsdpConfig
{
    public string DefaultVerificationPreset { get; set; } = "fast";
    public Dictionary<string, List<string>> VerificationPresets { get; set; } = new(StringComparer.Ordinal)
    {
        ["fast"] = ["echo \"configure verificationPresets in .jsdp/config.json\""],
        ["full"] = ["echo \"configure verificationPresets in .jsdp/config.json\""],
    };
    public JsdpRepoScanOptions RepoScan { get; set; } = new();
}

public sealed class JsdpRepoScanOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxDepth { get; set; } = 4;
}

public sealed class RepoScanSnapshot
{
    public string ScannedAt { get; set; } = "";
    public string WorkspaceRoot { get; set; } = "";
    public List<string> SolutionFiles { get; set; } = [];
    public List<string> ProjectFiles { get; set; } = [];
    public List<string> TopLevelDirectories { get; set; } = [];
    public List<string> ScriptFiles { get; set; } = [];
    public List<string> KeyFiles { get; set; } = [];
    public string? PrimaryStack { get; set; }
    public List<string> SuggestedVerification { get; set; } = [];
    public List<string> SuggestedMutationSurfaces { get; set; } = [];
}
