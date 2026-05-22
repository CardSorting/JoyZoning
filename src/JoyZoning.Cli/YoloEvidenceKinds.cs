namespace JoyZoning.Cli;

public static class YoloEvidenceKinds
{
    public const string Started = "yolo.started";
    public const string TaskSelected = "yolo.task.selected";
    public const string TaskSkipped = "yolo.task.skipped";
    public const string DispatchAttempted = "yolo.dispatch.attempted";
    public const string RecoverAttempted = "yolo.recover.attempted";
    public const string DispatchRetryAttempted = "yolo.dispatch.retry.attempted";
    public const string ExecutorWait = "yolo.executor.wait";
    public const string VerifyRetry = "yolo.verify.retry";
    public const string Blocked = "yolo.blocked";
    public const string ReadyForReview = "yolo.ready_for_review";
    public const string Stopped = "yolo.stopped";
    public const string PolicyViolation = "yolo.policy.violation";
}
