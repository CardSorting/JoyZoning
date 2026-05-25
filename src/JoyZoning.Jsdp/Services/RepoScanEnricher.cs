using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public sealed class RepoScanEnricher
{
    private static readonly string[] IgnoredDirs =
    [
        ".git", ".jsdp", "node_modules", "bin", "obj", "dist", ".next", "venv", ".venv",
    ];

    public RepoScanSnapshot Scan(string workspaceRoot, JsdpRepoScanOptions options)
    {
        var snapshot = new RepoScanSnapshot
        {
            ScannedAt = DateTimeOffset.UtcNow.ToString("O"),
            WorkspaceRoot = workspaceRoot,
        };

        if (!options.Enabled)
            return snapshot;

        foreach (var file in Directory.EnumerateFiles(workspaceRoot, "*.sln", SearchOption.TopDirectoryOnly))
            snapshot.SolutionFiles.Add(Rel(file, workspaceRoot));

        foreach (var file in Directory.EnumerateFiles(workspaceRoot, "*.csproj", SearchOption.AllDirectories))
        {
            if (IsIgnoredPath(file, workspaceRoot, options.MaxDepth))
                continue;
            snapshot.ProjectFiles.Add(Rel(file, workspaceRoot));
            if (snapshot.ProjectFiles.Count >= 32)
                break;
        }

        foreach (var dir in Directory.EnumerateDirectories(workspaceRoot))
        {
            var name = Path.GetFileName(dir);
            if (IgnoredDirs.Contains(name, StringComparer.OrdinalIgnoreCase))
                continue;
            snapshot.TopLevelDirectories.Add(name + "/");
        }

        var scriptsDir = Path.Combine(workspaceRoot, "scripts");
        if (Directory.Exists(scriptsDir))
        {
            foreach (var script in Directory.EnumerateFiles(scriptsDir, "*.*", SearchOption.TopDirectoryOnly))
            {
                if (script.EndsWith(".sh", StringComparison.OrdinalIgnoreCase) ||
                    script.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                    snapshot.ScriptFiles.Add(Rel(script, workspaceRoot));
            }
        }

        foreach (var key in new[] { "README.md", "package.json", "global.json", "JoyZoning.sln" })
        {
            var path = Path.Combine(workspaceRoot, key);
            if (File.Exists(path))
                snapshot.KeyFiles.Add(key);
        }

        snapshot.PrimaryStack = DetectStack(snapshot);
        snapshot.SuggestedVerification = BuildSuggestedVerification(workspaceRoot, snapshot);
        snapshot.SuggestedMutationSurfaces = BuildSuggestedSurfaces(snapshot);
        return snapshot;
    }

    public ProjectSpecAnalysis Enrich(ProjectSpecAnalysis analysis, RepoScanSnapshot scan)
    {
        if (scan.SolutionFiles.Count > 0 && !analysis.TechStack.Any(s => s.Contains(".sln", StringComparison.OrdinalIgnoreCase)))
            analysis.TechStack.Insert(0, $"Solution: {scan.SolutionFiles[0]}");

        foreach (var dir in scan.TopLevelDirectories.Take(8))
        {
            if (!analysis.CoreSystems.Any(s => s.Contains(dir.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)))
                analysis.CoreSystems.Add(dir.TrimEnd('/'));
        }

        if (analysis.VerificationStrategy.Count == 0 && scan.SuggestedVerification.Count > 0)
            analysis.VerificationStrategy.AddRange(scan.SuggestedVerification);

        foreach (var concept in scan.KeyFiles.Where(f => f != "README.md"))
        {
            if (!analysis.DomainConcepts.Contains(concept, StringComparer.OrdinalIgnoreCase))
                analysis.DomainConcepts.Add(concept);
        }

        return analysis;
    }

    public JsdpConfig BuildDefaultConfig(string workspaceRoot, RepoScanSnapshot scan)
    {
        var fast = scan.SuggestedVerification.Take(3).ToList();
        if (fast.Count == 0)
            fast = ["echo \"no verification detected — set verificationPresets in .jsdp/config.json\""];

        var full = scan.SuggestedVerification.Count > 0
            ? scan.SuggestedVerification
            : fast;

        return new JsdpConfig
        {
            DefaultVerificationPreset = "fast",
            VerificationPresets = new Dictionary<string, List<string>>(StringComparer.Ordinal)
            {
                ["fast"] = fast,
                ["full"] = full,
            },
            RepoScan = new JsdpRepoScanOptions { Enabled = true, MaxDepth = 4 },
        };
    }

    private static List<string> BuildSuggestedVerification(string workspaceRoot, RepoScanSnapshot scan)
    {
        var commands = new List<string>();

        if (scan.SolutionFiles.Count > 0)
        {
            var sln = scan.SolutionFiles[0];
            commands.Add($"dotnet build {sln}");
            var testProj = scan.ProjectFiles.FirstOrDefault(p =>
                p.Contains("Tests", StringComparison.OrdinalIgnoreCase) ||
                p.Contains(".Tests.", StringComparison.OrdinalIgnoreCase));
            if (testProj is not null)
                commands.Add($"dotnet test {testProj}");
        }
        else if (File.Exists(Path.Combine(workspaceRoot, "package.json")))
        {
            commands.Add("npm run typecheck");
            commands.Add("npm test");
        }

        var fastScript = scan.ScriptFiles.FirstOrDefault(s => s.Contains("run-tests", StringComparison.OrdinalIgnoreCase));
        if (fastScript is not null)
            commands.Add($"./{fastScript} fast");

        return commands.Distinct(StringComparer.Ordinal).Take(5).ToList();
    }

    private static List<string> BuildSuggestedSurfaces(RepoScanSnapshot scan)
    {
        var surfaces = scan.TopLevelDirectories
            .Where(d => d is "src/" or "tests/" or "apps/" or "docs/" or "scripts/")
            .ToList();

        if (surfaces.Count == 0)
            surfaces.AddRange(scan.TopLevelDirectories.Take(4));

        return surfaces.Count > 0 ? surfaces : ["src/", "tests/"];
    }

    private static string? DetectStack(RepoScanSnapshot scan)
    {
        if (scan.SolutionFiles.Count > 0 || scan.ProjectFiles.Count > 0)
            return "dotnet";
        if (scan.KeyFiles.Contains("package.json", StringComparer.OrdinalIgnoreCase))
            return "node";
        return null;
    }

    private static bool IsIgnoredPath(string path, string root, int maxDepth)
    {
        var rel = Path.GetRelativePath(root, path);
        var depth = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length;
        if (depth > maxDepth)
            return true;
        return rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => IgnoredDirs.Contains(part, StringComparer.OrdinalIgnoreCase));
    }

    private static string Rel(string path, string root) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
