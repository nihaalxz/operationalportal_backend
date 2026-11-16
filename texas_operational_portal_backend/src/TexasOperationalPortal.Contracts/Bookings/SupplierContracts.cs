using System.Collections.Generic;

namespace TexasOperationalPortal.Contracts.Bookings;

public sealed class SupplierResponse
{
    public long Id { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public List<string> Categories { get; init; } = new();
}
