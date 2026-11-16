using System;

namespace TexasOperationalPortal.Domain.Entities;

public sealed class OperationalAssignmentHistory
{
    public long Id { get; set; }

    /// <summary>
    /// Associated assignment ID.
    /// </summary>
    public long AssignmentId { get; set; }

    /// <summary>
    /// Action: "Claimed", "Released", "Reassigned", "Completed"
    /// </summary>
    public string Action { get; set; } = null!;

    /// <summary>
    /// Staff member who performed the action.
    /// </summary>
    public long PerformedByUserId { get; set; }

    /// <summary>
    /// Timestamp when action occurred.
    /// </summary>
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Previous assigned user ID (if reassigning/releasing).
    /// </summary>
    public long? OldAssignedUserId { get; set; }

    /// <summary>
    /// New assigned user ID (if claiming/reassigning).
    /// </summary>
    public long? NewAssignedUserId { get; set; }

    /// <summary>
    /// Audit reason / notes.
    /// </summary>
    public string? Reason { get; set; }

    // Navigation properties
    public OperationalAssignment Assignment { get; set; } = null!;
    public OperationalUser PerformedByUser { get; set; } = null!;
    public OperationalUser? OldAssignedUser { get; set; }
    public OperationalUser? NewAssignedUser { get; set; }
}
