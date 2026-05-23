using JoyZoning.Domain.Orchestration;

namespace JoyZoning.ControlPlane.Services;

internal static class MergeQueueApiMapper
{
    public static object ToJson(MergeQueueResponse model) => new
    {
        sessionId = model.SessionId,
        sessionWorkspaceRoot = model.SessionWorkspaceRoot,
        updatedAt = model.UpdatedAt,
        readyToMerge = model.ReadyToMerge.Select(MapWorker),
        mergeConflicts = model.MergeConflicts.Select(MapWorker),
        completedWorkers = model.CompletedWorkers.Select(MapWorker),
        revokedAbandoned = model.RevokedAbandoned.Select(MapWorker),
        allWorkers = model.AllWorkers.Select(MapWorker),
        warnings = model.Warnings.Select(w => new
        {
            w.Code,
            w.Message,
            w.LeaseId,
            w.TaskId,
        }),
    };

    private static object MapWorker(ParallelWorkerMirrorEntry w) =>
        ParallelWorkersApiMapper.MapWorker(w);
}
