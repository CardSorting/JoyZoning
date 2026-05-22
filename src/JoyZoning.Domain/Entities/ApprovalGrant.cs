using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Entities;

/// <summary>Pre-approved action scope for a task (approve-for-task).</summary>
public class ApprovalGrant
{
    public Guid Id { get; set; }
    public Guid WorkTaskId { get; set; }
    public ApprovalCategory Category { get; set; }
    public ApprovalScope Scope { get; set; }
    public string? CommandPattern { get; set; }
    public DateTimeOffset GrantedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }

    public WorkTask? WorkTask { get; set; }
}
