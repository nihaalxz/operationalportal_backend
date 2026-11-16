using System;

namespace TexasOperationalPortal.Domain.Entities;

public sealed class OperationalAssignment
{
    public long Id { get; set; }

    /// <summary>
    /// Type of operational entity (e.g. "Visa", "VFS", "Holiday").
    /// </summary>
    public string EntityType { get; set; } = null!;

    /// <summary>
    /// Foreign Id of entity in downstream service (Sales Portal).
    /// </summary>
    public long EntityId { get; set; }

    /// <summary>
    /// Human-readable document number for reference (e.g. TXSVISA891386).
    /// </summary>
    public string? EntityDocumentNumber { get; set; }

    /// <summary>
    /// Id of assigned operational staff member.
    /// </summary>
    public long AssignedToUserId { get; set; }

    /// <summary>
    /// Timestamp when assignment was created/claimed.
    /// </summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Id of operational staff member who completed the operation.
    /// </summary>
    public long? CompletedByUserId { get; set; }

    /// <summary>
    /// Timestamp when operation processing was completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Assignment Status: "Assigned", "Completed", "Released"
    /// </summary>
    public string Status { get; set; } = "Assigned";

    /// <summary>
    /// Staff member who performed the last modification.
    /// </summary>
    public long LastModifiedByUserId { get; set; }

    /// <summary>
    /// Timestamp of last modification.
    /// </summary>
    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Remarks / Reason for assignment change.
    /// </summary>
    public string? Remarks { get; set; }

    // Navigation properties
    public OperationalUser AssignedToUser { get; set; } = null!;
    public OperationalUser? CompletedByUser { get; set; }
    public OperationalUser LastModifiedByUser { get; set; } = null!;
}
