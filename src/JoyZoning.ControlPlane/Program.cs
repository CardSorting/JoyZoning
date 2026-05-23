using System.Text.Json.Serialization;
using JoyZoning.Agents;
using JoyZoning.Agents.Hermes;
using JoyZoning.Adapters;
using JoyZoning.ControlPlane.Background;
using JoyZoning.ControlPlane.Endpoints;
using JoyZoning.ControlPlane.Hubs;
using JoyZoning.ControlPlane.Services;
using JoyZoning.Domain.Configuration;
using JoyZoning.Persistence;

var builder = WebApplication.CreateBuilder(args);

var dbPath = builder.Configuration["JoyZoning:DatabasePath"];
if (string.IsNullOrWhiteSpace(dbPath))
    dbPath = Environment.GetEnvironmentVariable("JOYZONING_DB_PATH");
if (string.IsNullOrWhiteSpace(dbPath))
{
    var dataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "JoyZoning");
    Directory.CreateDirectory(dataDir);
    dbPath = Path.Combine(dataDir, "joyzoning.db");
}

builder.Services.Configure<HermesOptions>(builder.Configuration.GetSection(HermesOptions.SectionName));
builder.Services.Configure<ControlPlaneOptions>(builder.Configuration.GetSection(ControlPlaneOptions.SectionName));
builder.Services.Configure<LeaseRuntimeOptions>(builder.Configuration.GetSection(LeaseRuntimeOptions.SectionName));
builder.Services.Configure<WorkspaceOptions>(builder.Configuration.GetSection(WorkspaceOptions.SectionName));
builder.Services.Configure<ExecutorOptions>(builder.Configuration.GetSection(ExecutorOptions.SectionName));
builder.Services.Configure<BroccoliQOptions>(builder.Configuration.GetSection(BroccoliQOptions.SectionName));
builder.Services.PostConfigure<BroccoliQOptions>(opts =>
{
    if (string.IsNullOrWhiteSpace(opts.DatabasePath))
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(dir))
            opts.DatabasePath = Path.Combine(dir, "broccoliq.db");
    }

    JoyZoning.ControlPlane.Services.BroccoliQPaths.EnsureDatabaseDirectory(opts.DatabasePath);
});

builder.Services.AddJoyZoningPersistence(dbPath);
builder.Services.AddSingleton<JoyZoning.Agents.Hermes.IDashboardTokenRefresher, DashboardTokenRefresher>();
builder.Services.AddJoyZoningAgents();
if (builder.Environment.IsEnvironment("Testing"))
    JoyZoning.ControlPlane.Testing.TestAgentHostSetup.ReplaceAgents(builder.Services);
builder.Services.AddJoyZoningAdapters();

builder.Services.AddScoped<EventIngestor>();
builder.Services.AddScoped<WorkspaceEventPublisher>();
builder.Services.AddScoped<WorkspaceLiveMirrorService>();
builder.Services.AddScoped<LeaseWorktreeMonitor>();
builder.Services.AddScoped<LeaseRuntimeService>();
builder.Services.AddScoped<KanbanExecutionOrchestrator>();
builder.Services.AddScoped<OrchestrationService>();
builder.Services.AddScoped<ApprovalService>();
builder.Services.AddScoped<ConfigService>();
builder.Services.AddSingleton<HermesConnectivityService>();
builder.Services.AddSingleton<HermesConnectorDiagnostics>();
builder.Services.AddScoped<HermesDashboardConnectivityService>();
builder.Services.AddSingleton<HermesRunEventConsumer>();
builder.Services.AddSingleton<KanbanSyncState>();
builder.Services.AddSingleton<BroccoliQRuntimeMetrics>();
builder.Services.AddSingleton<BroccoliQCoordinator>();
builder.Services.AddScoped<BroccoliQBackfillService>();
builder.Services.AddHttpClient(BroccoliQBridgeClient.HttpClientName, (sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BroccoliQOptions>>().Value;
    client.BaseAddress = new Uri(opts.BridgeListenUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromMilliseconds(Math.Max(100, opts.EventMirrorTimeoutMs));
});
builder.Services.AddSingleton<BroccoliQBridgeClient>();
builder.Services.AddSingleton<IBroccoliQBridge>(sp => sp.GetRequiredService<BroccoliQBridgeClient>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<BroccoliQBridgeClient>());
builder.Services.AddSingleton<BroccoliQProcessService>();
builder.Services.AddHostedService<BroccoliQWorkerHostedService>();
builder.Services.AddHostedService<KanbanAutoSyncHostedService>();
builder.Services.AddHostedService<LeaseReconciliationHostedService>();
builder.Services.AddHostedService<LeaseWorktreeMonitorHostedService>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://127.0.0.1", "http://localhost")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

app.Services.EnsureDatabaseCreated();

// Mark interrupted executions on startup
using (var scope = app.Services.CreateScope())
{
    var executions = scope.ServiceProvider.GetRequiredService<JoyZoning.Persistence.Repositories.IExecutionRepository>();
    var interrupted = await executions.ListInterruptedAsync();
    foreach (var ex in interrupted)
        await executions.UpdatePhaseAsync(ex.Id, JoyZoning.Domain.Enums.ExecutionPhase.Interrupted);
}

using (var scope = app.Services.CreateScope())
{
    var configSvc = scope.ServiceProvider.GetRequiredService<ConfigService>();
    await configSvc.BootstrapRuntimeAsync();
}

app.UseCors();
app.MapJoyZoningApi();
app.MapHub<OperatorHub>("/hubs/operator");

if (!app.Environment.IsEnvironment("Testing"))
{
    var listenUrl = builder.Configuration["ControlPlane:ListenUrl"] ?? "http://127.0.0.1:9470";
    app.Urls.Add(listenUrl);
}

app.Run();

public partial class Program;
