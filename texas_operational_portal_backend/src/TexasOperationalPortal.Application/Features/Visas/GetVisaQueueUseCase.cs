using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Contracts.Common;
using TexasOperationalPortal.Contracts.Bookings;

namespace TexasOperationalPortal.Application.Features.Visas;

public sealed class GetVisaQueueUseCase
{
    private readonly ISalesOperationsClient _salesClient;

    public GetVisaQueueUseCase(ISalesOperationsClient salesClient)
    {
        _salesClient = salesClient;
    }

    public Task<PagedResponse<VisaApplicationSummaryResponse>> ExecuteAsync(
        int page, int pageSize, string? status, string? search, CancellationToken ct)
    {
        return _salesClient.GetVisaApplicationsAsync(page, pageSize, status, search, ct);
    }
}
