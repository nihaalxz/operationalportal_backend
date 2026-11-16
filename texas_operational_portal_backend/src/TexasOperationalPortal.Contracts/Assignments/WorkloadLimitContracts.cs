using System;
using System.Collections.Generic;

namespace TexasOperationalPortal.Contracts.Assignments;

public record WorkloadLimitResponse(
    long Id,
    string EntityType,
    int MaxActiveLimit,
    long? UserId,
    DateTime UpdatedAt
);

public record UpdateWorkloadLimitRequest(
    string EntityType,
    int MaxActiveLimit,
    long? UserId = null
);

public record UpdateWorkloadLimitsBatchRequest(
    List<UpdateWorkloadLimitRequest> Limits
);

public record OperatorWorkloadSummaryResponse(
    int VisaActiveCount,
    int VisaMaxLimit,
    int VfsActiveCount,
    int VfsMaxLimit,
    int HolidayActiveCount,
    int HolidayMaxLimit
);
