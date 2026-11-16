using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Contracts.Bookings;

namespace TexasOperationalPortal.Application.Features.Holidays;

public sealed class GetHolidayDetailsUseCase
{
    private readonly ISalesOperationsClient _salesClient;

    public GetHolidayDetailsUseCase(ISalesOperationsClient salesClient)
    {
        _salesClient = salesClient;
    }

    public Task<HolidayPackageDetailResponse?> ExecuteAsync(long id, CancellationToken ct)
    {
        return _salesClient.GetHolidayPackageDetailsAsync(id, ct);
    }
}
