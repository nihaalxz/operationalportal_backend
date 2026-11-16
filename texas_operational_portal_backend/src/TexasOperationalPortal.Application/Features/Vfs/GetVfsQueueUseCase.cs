using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Contracts.Common;
using TexasOperationalPortal.Contracts.Bookings;

namespace TexasOperationalPortal.Application.Features.Vfs;

public sealed class GetVfsQueueUseCase
{
    private readonly ISalesOperationsClient _salesClient;

    public GetVfsQueueUseCase(ISalesOperationsClient salesClient)
    {
        _salesClient = salesClient;
    }

    public Task<PagedResponse<VfsAppointmentSummaryResponse>> ExecuteAsync(
        int page, int pageSize, string? status, string? search, CancellationToken ct)
    {
        return _salesClient.GetVfsAppointmentsAsync(page, pageSize, status, search, ct);
    }
}
