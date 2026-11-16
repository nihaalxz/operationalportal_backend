using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Contracts.Bookings;

namespace TexasOperationalPortal.Application.Features.Vfs;

public sealed class GetVfsDetailsUseCase
{
    private readonly ISalesOperationsClient _salesClient;

    public GetVfsDetailsUseCase(ISalesOperationsClient salesClient)
    {
        _salesClient = salesClient;
    }

    public Task<VfsAppointmentDetailResponse?> ExecuteAsync(long id, CancellationToken ct)
    {
        return _salesClient.GetVfsAppointmentDetailsAsync(id, ct);
    }
}
