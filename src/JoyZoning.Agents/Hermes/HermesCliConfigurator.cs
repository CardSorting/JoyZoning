using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace JoyZoning.Agents.Hermes;

/// <summary>Apply Hermes <c>config set</c> changes via the diet-hermes CLI.</summary>
public class HermesCliConfigurator
{
    private readonly ILogger<HermesCliConfigurator>? _logger;

    public HermesCliConfigurator(ILogger<HermesCliConfigurator>? logger = null) =>
        _logger = logger;

    public async Task<HermesModelSyncResult> SyncModelFromProfileAsync(
        string installRoot,
        string targetProfile,
        string sourceProfile,
        CancellationToken cancellationToken = default)
    {
        var hermesHome = HermesProfileCatalog.ResolveHermesHome();
        var source = HermesProfileCatalog.FindProfile(sourceProfile, hermesHome)
            ?? throw new InvalidOperationException($"Hermes profile '{sourceProfile}' not found under {hermesHome}.");
        if (!source.Model.IsConfigured)
            throw new InvalidOperationException($"Profile '{sourceProfile}' has no model configured in {source.ConfigPath}.");

        var hermes = HermesCliLocator.FindHermesExecutable(installRoot)
            ?? throw new InvalidOperationException(
                $"Hermes CLI not found under install root '{installRoot}'. Run jz doctor or set Hermes:InstallRoot.");

        var before = HermesProfileCatalog.FindProfile(targetProfile, hermesHome);

        var commands = BuildConfigSetCommands(source.Model);
        var log = new StringBuilder();
        foreach (var args in commands)
        {
            var output = await RunHermesAsync(hermes, installRoot, targetProfile, args, cancellationToken);
            log.AppendLine(output);
        }

        var after = HermesProfileCatalog.FindProfile(targetProfile, hermesHome);
        return new HermesModelSyncResult(
            SourceProfile: sourceProfile,
            TargetProfile: targetProfile,
            Before: before?.Model.Describe() ?? "(unknown)",
            After: after?.Model.Describe() ?? "(unknown)",
            Log: log.ToString().Trim());
    }

    public static IReadOnlyList<string> BuildConfigSetCommands(HermesModelConfig model)
    {
        if (model.Format == HermesModelFormat.InlineString && !string.IsNullOrWhiteSpace(model.Model))
            return new[] { $"model {model.Model}" };

        var commands = new List<string>();
        if (!string.IsNullOrWhiteSpace(model.Model))
            commands.Add($"model.default {model.Model}");
        if (!string.IsNullOrWhiteSpace(model.Provider))
            commands.Add($"model.provider {model.Provider}");
        if (!string.IsNullOrWhiteSpace(model.BaseUrl))
            commands.Add($"model.base_url {model.BaseUrl}");

        if (commands.Count == 0)
            throw new InvalidOperationException("No model fields to sync.");

        return commands;
    }

    private async Task<string> RunHermesAsync(
        string hermesExecutable,
        string installRoot,
        string profile,
        string configSetArgs,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = hermesExecutable,
            Arguments = $"-p {profile} config set {configSetArgs}",
            WorkingDirectory = installRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start hermes process.");

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var combined = string.Join(Environment.NewLine, new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s)));
        _logger?.LogDebug("hermes -p {Profile} config set {Args} -> {Exit}: {Output}", profile, configSetArgs, process.ExitCode, combined);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"hermes config set failed ({configSetArgs}): {combined}");

        return combined;
    }
}

public sealed record HermesModelSyncResult(
    string SourceProfile,
    string TargetProfile,
    string Before,
    string After,
    string Log);
