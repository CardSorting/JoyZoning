using JoyZoning.Domain.Entities;

namespace JoyZoning.Domain.Orchestration;

/// <summary>Result of accepting a ReadyForReview worker (POST …/lease/merge).</summary>
public sealed record AcceptResultResponse(
    WorkTask Task,
    WorkspaceConvergenceResult? GitConvergence,
    bool MetadataOnly);
