using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Contracts.Common;
using TexasOperationalPortal.Contracts.Bookings;

namespace TexasOperationalPortal.Application.Features.Holidays;

public sealed class GetHolidayQueueUseCase
{
    private readonly ISalesOperationsClient _salesClient;

    public GetHolidayQueueUseCase(ISalesOperationsClient salesClient)
    {
        _salesClient = salesClient;
    }

    public Task<PagedResponse<HolidayPackageSummaryResponse>> ExecuteAsync(
        int page, int pageSize, string? status, string? search, CancellationToken ct)
    {
        return _salesClient.GetHolidayPackagesAsync(page, pageSize, status, search, ct);
    }
}
