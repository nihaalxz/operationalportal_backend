using System.Collections.Generic;
using System.Threading.Tasks;
using TexasOperationalPortal.Contracts.Assignments;

namespace TexasOperationalPortal.Application.Common.Interfaces;

public interface IAssignmentCacheService
{
    Task<IReadOnlyList<OperationalAssignmentResponse>?> GetActiveAssignmentsAsync();
    Task SetActiveAssignmentsAsync(IReadOnlyList<OperationalAssignmentResponse> assignments);
    Task<MyAssignmentsSummaryResponse?> GetUserAssignmentsAsync(long userId);
    Task SetUserAssignmentsAsync(long userId, MyAssignmentsSummaryResponse summary);
    Task InvalidateAllCacheAsync();
    Task InvalidateUserCacheAsync(long userId);
    Task<IReadOnlyList<WorkloadLimitResponse>?> GetWorkloadLimitsAsync();
    Task SetWorkloadLimitsAsync(IReadOnlyList<WorkloadLimitResponse> limits);
    Task InvalidateWorkloadLimitsAsync();
}
