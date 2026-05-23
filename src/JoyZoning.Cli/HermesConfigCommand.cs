using JoyZoning.Agents.Hermes;

namespace JoyZoning.Cli;

public static class HermesConfigCommand
{
    public static async Task<int> RunAsync(JoyZoningCliClient client, CliContext ctx, string[] args)
    {
        if (args.Length == 0)
            return await ShowStatusAsync(client, ctx);

        return args[0].ToLowerInvariant() switch
        {
            "profiles" => await ListProfilesAsync(ctx),
            "use" => await UseProfileAsync(client, ctx, args),
            "sync-model" => await SyncModelAsync(client, ctx, args),
            _ => throw new CliUsageException("config hermes | profiles | use <name> | sync-model [--from default]"),
        };
    }

    private static async Task<int> ShowStatusAsync(JoyZoningCliClient client, CliContext ctx)
    {
        var linkedProfile = await ResolveLinkedProfileAsync(client);
        var diagnostic = HermesProfileCatalog.DiagnoseLink(linkedProfile);
        var profiles = HermesProfileCatalog.ListProfiles();

        var payload = new Dictionary<string, object?>
        {
            ["ok"] = diagnostic.ModelsMatch,
            ["linkedProfile"] = linkedProfile,
            ["linkedModel"] = diagnostic.LinkedProfile.Model.Describe(),
            ["defaultModel"] = diagnostic.ReferenceProfile.Model.Describe(),
            ["modelsMatch"] = diagnostic.ModelsMatch,
            ["recommendation"] = diagnostic.Recommendation,
            ["profiles"] = profiles.Select(p => new
            {
                name = p.Name,
                model = p.Model.Describe(),
                provider = p.Model.Provider,
                configExists = p.ConfigExists,
                apiServerPort = p.ApiServerPort,
                hasApiServerKey = p.HasApiServerKey,
            }).ToList(),
            ["commands"] = new[]
            {
                "jz config hermes sync-model --from default",
                "jz config hermes use default",
                "hermes -p joyzoning model",
            },
        };

        return CliOutput.WriteEnvelope(ctx, payload!);
    }

    private static Task<int> ListProfilesAsync(CliContext ctx)
    {
        var profiles = HermesProfileCatalog.ListProfiles();
        var payload = new
        {
            ok = true,
            profiles = profiles.Select(p => new
            {
                name = p.Name,
                model = p.Model.Describe(),
                configPath = p.ConfigPath,
                hasApiServerKey = p.HasApiServerKey,
                apiServerPort = p.ApiServerPort,
            }),
        };
        return Task.FromResult(CliOutput.WriteEnvelope(ctx, payload));
    }

    private static async Task<int> UseProfileAsync(JoyZoningCliClient client, CliContext ctx, string[] args)
    {
        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            throw new CliUsageException("config hermes use <profile-name>");

        var profileName = args[1].Trim();
        var found = HermesProfileCatalog.FindProfile(profileName);
        if (found is null)
            throw new CliUsageException($"Hermes profile '{profileName}' not found under {HermesProfileCatalog.ResolveHermesHome()}.");

        var current = await client.GetConfigAsync();
        if (!current.IsSuccess || !current.Body.HasValue)
            throw new CliUsageException($"Could not read control plane config: {current.Message}");

        var body = current.Body.Value;
        var installRoot = body.GetProperty("installRoot").GetString()
            ?? throw new CliUsageException("installRoot missing from control plane config.");
        var apiBaseUrl = body.TryGetProperty("apiBaseUrl", out var urlProp)
            ? urlProp.GetString()
            : $"http://127.0.0.1:{found.ApiServerPort}";
        var dashboardBaseUrl = body.TryGetProperty("dashboardBaseUrl", out var dashProp)
            ? dashProp.GetString()
            : "http://127.0.0.1:9119";

        if (found.ApiServerPort != 8642 && apiBaseUrl?.EndsWith(":8642", StringComparison.Ordinal) == true)
            apiBaseUrl = $"http://127.0.0.1:{found.ApiServerPort}";

        var put = await client.SaveConfigAsync(new
        {
            installRoot,
            apiBaseUrl,
            dashboardBaseUrl,
            profile = profileName,
            dashboardSessionToken = body.TryGetProperty("dashboardSessionToken", out var tok)
                ? tok.GetString() ?? ""
                : "",
            autoSyncEnabled = body.TryGetProperty("autoSyncEnabled", out var sync) && sync.GetBoolean(),
            autoSyncIntervalSeconds = body.TryGetProperty("autoSyncIntervalSeconds", out var interval)
                ? interval.GetInt32()
                : 120,
        });

        if (!put.IsSuccess)
            return CliOutput.WriteResult(ctx, put);

        return CliOutput.WriteEnvelope(ctx, new
        {
            ok = true,
            profile = profileName,
            model = found.Model.Describe(),
            apiBaseUrl,
            message = profileName.Equals("default", StringComparison.OrdinalIgnoreCase)
                ? "JoyZoning now uses the default Hermes profile. Ensure API_SERVER_ENABLED is set in ~/.hermes/.env if dispatch fails."
                : $"JoyZoning now uses Hermes profile '{profileName}'. Restart the gateway if it was already running.",
        });
    }

    private static async Task<int> SyncModelAsync(JoyZoningCliClient client, CliContext ctx, string[] args)
    {
        var fromProfile = CliArgs.OptStatic(ctx.Args.Raw, "--from") ?? "default";
        var targetProfile = await ResolveLinkedProfileAsync(client);

        var config = await client.GetConfigAsync();
        if (!config.IsSuccess || !config.Body.HasValue)
            throw new CliUsageException($"Could not read control plane config: {config.Message}");

        var installRoot = config.Body.Value.GetProperty("installRoot").GetString()
            ?? Environment.GetEnvironmentVariable("JOYZONING_DIET_HERMES_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "diet-hermes-main-master");

        var syncer = new HermesCliConfigurator();
        var result = await syncer.SyncModelFromProfileAsync(installRoot, targetProfile, fromProfile);

        return CliOutput.WriteEnvelope(ctx, new
        {
            ok = true,
            result.SourceProfile,
            result.TargetProfile,
            before = result.Before,
            after = result.After,
            log = result.Log,
            message = $"Copied model from '{fromProfile}' into '{targetProfile}'. Restart: hermes -p {targetProfile} gateway",
        });
    }

    private static async Task<string> ResolveLinkedProfileAsync(JoyZoningCliClient client)
    {
        var envProfile = Environment.GetEnvironmentVariable("JOYZONING_HERMES_PROFILE");
        if (!string.IsNullOrWhiteSpace(envProfile))
            return envProfile.Trim();

        var config = await client.GetConfigAsync();
        if (config.IsSuccess && config.Body.HasValue && config.Body.Value.TryGetProperty("profile", out var profileProp))
        {
            var fromApi = profileProp.GetString();
            if (!string.IsNullOrWhiteSpace(fromApi))
                return fromApi;
        }

        return "joyzoning";
    }
}
