using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Contracts.Assignments;

namespace TexasOperationalPortal.Application.Features.Assignments;

public interface IAssignmentService
{
    Task<OperationalAssignmentResponse> ClaimOperationAsync(
        ClaimAssignmentRequest request, long userId, CancellationToken ct);

    Task<OperationalAssignmentResponse> ReleaseOperationAsync(
        long assignmentId, string? reason, long performedByUserId, bool isAdminOrAccounts, CancellationToken ct);

    Task<OperationalAssignmentResponse> ReassignOperationAsync(
        long assignmentId, long targetUserId, string? reason, long performedByUserId, bool isAdminOrAccounts, CancellationToken ct);

    Task CompleteOperationAsync(
        string entityType, long entityId, long completedByUserId, CancellationToken ct);

    Task<MyAssignmentsSummaryResponse> GetMyAssignmentsAsync(
        long userId, CancellationToken ct);

    Task<IReadOnlyList<OperationalAssignmentResponse>> GetAllActiveAssignmentsAsync(
        CancellationToken ct);

    Task<IReadOnlyList<OperationalAssignmentHistoryResponse>> GetAssignmentHistoryAsync(
        long assignmentId, CancellationToken ct);

    Task<IReadOnlyList<OperationalAssignmentResponse>> GetActiveAssignmentsForEntitiesAsync(
        string entityType, IEnumerable<long> entityIds, CancellationToken ct);

    Task<IReadOnlyList<OperationalAssignmentResponse>> GetCompletedAssignmentsAsync(
        long userId, CancellationToken ct);

    Task<IReadOnlyList<WorkloadLimitResponse>> GetWorkloadLimitsAsync(
        CancellationToken ct);

    Task<IReadOnlyList<WorkloadLimitResponse>> UpdateWorkloadLimitsAsync(
        UpdateWorkloadLimitsBatchRequest request, long performedByUserId, CancellationToken ct);

    Task<OperatorWorkloadSummaryResponse> GetOperatorWorkloadSummaryAsync(
        long userId, CancellationToken ct);
}
