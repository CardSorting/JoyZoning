using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

/// <summary>Caller identity for lease mutations (scheduling + ownership).</summary>
public sealed record LeaseOperationContext(
    Guid SessionId,
    StatusChangeActor Actor,
    bool RecoveryFlow = false);
