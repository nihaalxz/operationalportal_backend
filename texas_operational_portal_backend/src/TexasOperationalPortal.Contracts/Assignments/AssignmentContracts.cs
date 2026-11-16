using System;

namespace TexasOperationalPortal.Contracts.Assignments;

public sealed record ClaimAssignmentRequest(
    string EntityType,
    long EntityId,
    string? EntityDocumentNumber,
    string? Remarks
);

public sealed record ReassignAssignmentRequest(
    long TargetUserId,
    string? Reason
);

public sealed record ReleaseAssignmentRequest(
    string? Reason
);

public sealed record OperationalAssignmentResponse(
    long Id,
    string EntityType,
    long EntityId,
    string? EntityDocumentNumber,
    long AssignedToUserId,
    string AssignedToUserName,
    DateTime AssignedAt,
    long? CompletedByUserId,
    string? CompletedByUserName,
    DateTime? CompletedAt,
    string Status,
    long LastModifiedByUserId,
    string LastModifiedByUserName,
    DateTime LastModifiedAt,
    string? Remarks
);

public sealed record OperationalAssignmentHistoryResponse(
    long Id,
    long AssignmentId,
    string Action,
    long PerformedByUserId,
    string PerformedByUserName,
    DateTime PerformedAt,
    long? OldAssignedUserId,
    string? OldAssignedUserName,
    long? NewAssignedUserId,
    string? NewAssignedUserName,
    string? Reason
);

public sealed record MyAssignmentsSummaryResponse(
    int TotalActiveCount,
    System.Collections.Generic.IReadOnlyList<OperationalAssignmentResponse> Assignments
);
