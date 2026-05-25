using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JoyZoning.ControlPlane.Services;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Tests.Infrastructure;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class HabitatLayerBoundaryTests
{
    [Fact]
    public void Habitat_rejects_authoritative_observations()
    {
        Assert.True(HabitatLayerGuardrails.RejectsAuthoritativeObservation(true));
        Assert.False(HabitatLayerGuardrails.RejectsAuthoritativeObservation(false));
    }

    [Theory]
    [InlineData("operator.review.opened")]
    [InlineData("lease.granted")]
    [InlineData("merge.accepted")]
    [InlineData("verification.report.attached")]
    [InlineData("habitat.ui.mode_changed")]
    public void Habitat_may_emit_operator_and_merge_events(string eventType)
    {
        Assert.True(HabitatLayerGuardrails.HabitatMayEmitEventType(eventType));
        Assert.False(HabitatLayerGuardrails.HabitatMustNotEmitEventType(eventType));
    }

    [Theory]
    [InlineData("tool.terminal")]
    [InlineData("terminal.exec")]
    [InlineData("mutation.patch_applied")]
    [InlineData("convergence.converged")]
    [InlineData("convergence.ready_for_review")]
    public void Habitat_must_not_emit_execution_or_runtime_convergence_events(string eventType)
    {
        Assert.True(HabitatLayerGuardrails.HabitatMustNotEmitEventType(eventType));
    }

    [Fact]
    public void Hermes_observation_read_model_is_non_authoritative_display_only()
    {
        var model = new HermesObservationReadModel();
        var scope = Guid.NewGuid().ToString();
        model.ApplyObservation(new HermesObservationSnapshot(
            "convergence.ready_for_review",
            "convergence",
            scope,
            null,
            "run-1",
            "ready_for_review",
            DateTimeOffset.UtcNow));

        var state = model.GetConvergence(scope);
        Assert.NotNull(state);
        Assert.Equal("ready_for_review", state!.State);
        Assert.Equal("convergence.ready_for_review", state.LastEventType);
    }

    [Fact]
    public void Hermes_journal_adapter_documents_canonical_owner()
    {
        Assert.Contains("Hermes-owned", HermesJournalAdapter.CanonicalJournalHint);
        Assert.Contains("mirrored", HermesJournalAdapter.DisplayNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Hermes_observation_dedupe_rejects_duplicate_within_ttl()
    {
        var dedupe = new HermesObservationDedupe();
        const string key = "convergence.ready_for_review|scope|run|123";
        Assert.True(dedupe.TryAccept(key, out _));
        Assert.False(dedupe.TryAccept(key, out var reason));
        Assert.Equal("duplicate_observation", reason);
    }
}

[Trait(TestCategories.Key, TestCategories.Integration)]
[Collection("OrchestrationApi")]
public class HermesObservationApiIntegrationTests : IAsyncLifetime
{
    private readonly JoyZoningApiCollectionFixture _fixture;

    public HermesObservationApiIntegrationTests(JoyZoningApiCollectionFixture fixture) =>
        _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Hermes_observation_ingest_rejects_authoritative_payload()
    {
        var scope = Guid.NewGuid();
        var resp = await _fixture.Client.PostAsJsonAsync("/api/internal/hermes-observation", new
        {
            type = "convergence.ready_for_review",
            layer = "convergence",
            scopeId = scope.ToString(),
            sessionId = (string?)null,
            runId = "run-1",
            payload = new { state = "ready_for_review" },
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            source = HabitatLayerGuardrails.HermesRuntimeSource,
            authoritative = true,
        });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Hermes_observation_ingest_and_convergence_query_are_observe_only()
    {
        var scope = Guid.NewGuid();
        var ingest = await _fixture.Client.PostAsJsonAsync("/api/internal/hermes-observation", new
        {
            type = "convergence.ready_for_review",
            layer = "convergence",
            scopeId = scope.ToString(),
            sessionId = (string?)null,
            runId = "run-1",
            payload = new { state = "ready_for_review" },
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            source = HabitatLayerGuardrails.HermesRuntimeSource,
            authoritative = false,
        });

        Assert.Equal(HttpStatusCode.Accepted, ingest.StatusCode);

        var query = await _fixture.Client.GetFromJsonAsync<JsonElement>(
            $"/api/hermes/convergence/{scope}");
        Assert.True(query.GetProperty("observed").GetBoolean());
        Assert.False(query.GetProperty("authoritative").GetBoolean());
        Assert.Equal("ready_for_review", query.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Hermes_observation_e2e_flow_matches_watch_convergence_contract()
    {
        var scope = Guid.NewGuid();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var reject = await _fixture.Client.PostAsJsonAsync("/api/internal/hermes-observation", new
        {
            type = "convergence.ready_for_review",
            layer = "convergence",
            scopeId = scope.ToString(),
            sessionId = (string?)null,
            runId = "run-e2e",
            payload = new { state = "ready_for_review" },
            timestamp = ts,
            source = HabitatLayerGuardrails.HermesRuntimeSource,
            authoritative = true,
        });
        Assert.Equal(HttpStatusCode.Forbidden, reject.StatusCode);

        var ingest = await _fixture.Client.PostAsJsonAsync("/api/internal/hermes-observation", new
        {
            type = "convergence.ready_for_review",
            layer = "convergence",
            scopeId = scope.ToString(),
            sessionId = (string?)null,
            runId = "run-e2e",
            payload = new { state = "ready_for_review" },
            timestamp = ts,
            source = HabitatLayerGuardrails.HermesRuntimeSource,
            authoritative = false,
        });
        ingest.EnsureSuccessStatusCode();

        var query = await _fixture.Client.GetFromJsonAsync<JsonElement>(
            $"/api/hermes/convergence/{scope}");
        Assert.True(query.GetProperty("observed").GetBoolean());
        Assert.False(query.GetProperty("authoritative").GetBoolean());
        Assert.Equal("ready_for_review", query.GetProperty("state").GetString());
        Assert.Equal("convergence.ready_for_review", query.GetProperty("lastEventType").GetString());
        Assert.Contains("mirrored", query.GetProperty("note").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Hermes_observation_ingest_rejects_non_hermes_source()
    {
        var scope = Guid.NewGuid();
        var resp = await _fixture.Client.PostAsJsonAsync("/api/internal/hermes-observation", new
        {
            type = "convergence.ready_for_review",
            layer = "convergence",
            scopeId = scope.ToString(),
            sessionId = (string?)null,
            runId = "run-1",
            payload = new { state = "ready_for_review" },
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            source = "joyzoning-habitat",
            authoritative = false,
        });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Hermes_observation_ingest_does_not_merge_tasks()
    {
        var scope = Guid.NewGuid();
        await _fixture.Client.PostAsJsonAsync("/api/internal/hermes-observation", new
        {
            type = "convergence.converged",
            layer = "convergence",
            scopeId = scope.ToString(),
            sessionId = (string?)null,
            runId = "run-1",
            payload = new { state = "converged" },
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            source = HabitatLayerGuardrails.HermesRuntimeSource,
            authoritative = false,
        });

        var query = await _fixture.Client.GetFromJsonAsync<JsonElement>(
            $"/api/hermes/convergence/{scope}");
        Assert.True(query.GetProperty("observed").GetBoolean());
        Assert.False(query.GetProperty("authoritative").GetBoolean());
    }

    [Fact]
    public async Task Habitat_authority_checklist_exposes_runtime_and_habitat_roles()
    {
        var checklist = await _fixture.Client.GetFromJsonAsync<JsonElement>(
            "/api/habitat/authority-checklist");
        Assert.Equal("hermes", checklist.GetProperty("runtimeOwner").GetString());
        Assert.Equal("observe-only", checklist.GetProperty("habitatRole").GetString());
        Assert.True(checklist.GetProperty("items").GetArrayLength() >= 5);
    }
}
