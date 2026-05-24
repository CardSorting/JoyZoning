using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace JoyZoning.Domain.Orchestration;

/// <summary>Persists the last known-good agent manifest for offline agent bootstrap.</summary>
public static class AgentOperationsManifestCache
{
    public const string RelativePath = ".joyzoning/agent-manifest.json";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string ComputeStaticFingerprint() =>
        $"{AgentOperationsManifest.ManifestVersion}|endpoints={JoyZoningEndpointRegistry.Endpoints.Count}|files={AgentOperationsManifest.ImportantFiles.Count}|commands={AgentOperationsManifest.Commands.Count}";

    public static string ComputeWorkspaceFingerprint(string workspaceRoot)
    {
        var sync = JoyZoningEndpointRegistrySync.CompareRegistryToApiFile(workspaceRoot);
        return $"{ComputeStaticFingerprint()}|sync={(sync.Ok ? "ok" : "stale")}|api={sync.ApiRouteCount}";
    }

    public static string AbsolutePath(string workspaceRoot) =>
        Path.Combine(workspaceRoot, RelativePath);

    public static bool Exists(string workspaceRoot) =>
        File.Exists(AbsolutePath(workspaceRoot));

    public static bool TryReadFingerprint(string workspaceRoot, out string? fingerprint)
    {
        fingerprint = null;
        var path = AbsolutePath(workspaceRoot);
        if (!File.Exists(path))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("fingerprint", out var fp))
                fingerprint = fp.GetString();
            return !string.IsNullOrWhiteSpace(fingerprint);
        }
        catch
        {
            return false;
        }
    }

    public static void Write(string workspaceRoot, object manifest)
    {
        var dir = Path.Combine(workspaceRoot, ".joyzoning");
        Directory.CreateDirectory(dir);
        File.WriteAllText(AbsolutePath(workspaceRoot), JsonSerializer.Serialize(manifest, WriteOptions));
    }

    public static string HashImportantFiles(string workspaceRoot)
    {
        var sb = new StringBuilder();
        foreach (var relative in AgentOperationsManifest.ImportantFiles)
        {
            var full = Path.Combine(workspaceRoot, relative);
            sb.Append(relative);
            sb.Append('|');
            sb.Append(File.Exists(full) ? File.GetLastWriteTimeUtc(full).Ticks : 0);
            sb.Append(';');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
