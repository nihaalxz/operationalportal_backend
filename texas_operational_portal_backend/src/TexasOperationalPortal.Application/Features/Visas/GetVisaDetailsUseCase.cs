using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Contracts.Bookings;

namespace TexasOperationalPortal.Application.Features.Visas;

public sealed class GetVisaDetailsUseCase
{
    private readonly ISalesOperationsClient _salesClient;

    public GetVisaDetailsUseCase(ISalesOperationsClient salesClient)
    {
        _salesClient = salesClient;
    }

    public Task<VisaApplicationDetailResponse?> ExecuteAsync(long id, CancellationToken ct)
    {
        return _salesClient.GetVisaApplicationDetailsAsync(id, ct);
    }
}
