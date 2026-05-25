using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public sealed class JSDPLedger
{
    private readonly string _workspaceRoot;

    public JSDPLedger(string workspaceRoot) => _workspaceRoot = workspaceRoot;

    public IReadOnlyList<LedgerEntry> ReadAll()
    {
        var path = JsdpPaths.Ledger(_workspaceRoot);
        if (!File.Exists(path))
            return [];

        var entries = new List<LedgerEntry>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            entries.Add(System.Text.Json.JsonSerializer.Deserialize<LedgerEntry>(line, JsdpJson.Options)!);
        }

        return entries;
    }

    public void Append(LedgerEntry entry)
    {
        Directory.CreateDirectory(JsdpPaths.Root(_workspaceRoot));
        var path = JsdpPaths.Ledger(_workspaceRoot);
        var line = System.Text.Json.JsonSerializer.Serialize(entry, JsdpJson.CompactOptions);
        File.AppendAllText(path, line + Environment.NewLine);
    }

    public int EntryCount() =>
        File.Exists(JsdpPaths.Ledger(_workspaceRoot))
            ? File.ReadLines(JsdpPaths.Ledger(_workspaceRoot)).Count(l => !string.IsNullOrWhiteSpace(l))
            : 0;
}
