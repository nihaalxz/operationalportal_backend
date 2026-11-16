using System;

namespace TexasOperationalPortal.Contracts.Bookings;

public sealed record HolidayPackageSummaryResponse
{
    public long Id { get; init; }
    public string? DocumentNumber { get; init; }
    public string? HeadName { get; init; }
    public string PassengerName { get; init; } = null!;
    public string Country { get; init; } = null!;
    public int Days { get; init; }
    public string PaymentStatus { get; init; } = null!;
    public string ProcessingStatus { get; init; } = null!;
    public decimal PayableAmount { get; init; }
    public decimal Profit { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public sealed record HolidayPackageDetailResponse
{
    public long Id { get; init; }
    public string? DocumentNumber { get; init; }
    public long CreatedBy { get; init; }
    public long? HeadId { get; init; }
    public string? HeadName { get; init; }
    public string? ClientType { get; init; }
    public string? Agent { get; init; }
    public long? AgentId { get; init; }
    public string Prefix { get; init; } = null!;
    public string PassengerName { get; init; } = null!;
    public string Supplier { get; init; } = null!;
    public long? SupplierId { get; init; }
    public string Country { get; init; } = null!;
    public int Days { get; init; }
    public decimal PayableAmount { get; init; }
    public decimal ExpectedSupplierCost { get; init; }
    public decimal Profit { get; init; }
    public string PaymentMode { get; init; } = null!;
    public string PaymentStatus { get; init; } = null!;
    public string ProcessingStatus { get; init; } = null!;
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public System.Collections.Generic.List<HolidayPackageDocumentResponse> Documents { get; init; } = new();
    public System.Collections.Generic.List<GovtFeeDetailResponse> GovtFees { get; init; } = new();
}

public sealed record HolidayPackageDocumentResponse(
    long Id,
    string DocumentPath,
    string DocumentType,
    DateTime? CreatedAt
);
