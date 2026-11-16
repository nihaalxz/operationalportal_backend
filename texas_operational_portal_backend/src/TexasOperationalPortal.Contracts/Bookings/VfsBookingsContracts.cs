using System;

namespace TexasOperationalPortal.Contracts.Bookings;

public sealed record VfsAppointmentSummaryResponse
{
    public long Id { get; init; }
    public string? DocumentNumber { get; init; }
    public string? HeadName { get; init; }
    public string Name { get; init; } = null!;
    public string Country { get; init; } = null!;
    public string VisaType { get; init; } = null!;
    public string PaymentStatus { get; init; } = null!;
    public string ProcessingStatus { get; init; } = null!;
    public decimal PayableAmount { get; init; }
    public decimal Profit { get; init; }
    public DateTime? AppointmentDate { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public sealed record VfsAppointmentDetailResponse
{
    public long Id { get; init; }
    public string? DocumentNumber { get; init; }
    public long CreatedBy { get; init; }
    public long? HeadId { get; init; }
    public string? HeadName { get; init; }
    public string? ClientType { get; init; }
    public string? Agent { get; init; }
    public long? AgentId { get; init; }
    public string VfsId { get; init; } = null!;
    public string? VfsAppId { get; init; }
    public string Prefix { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Supplier { get; init; } = null!;
    public long? SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public string Country { get; init; } = null!;
    public string VisaType { get; init; } = null!;
    public decimal PayableAmount { get; init; }
    public decimal ExpectedSupplierCost { get; init; }
    public decimal Profit { get; init; }
    public string PaymentMode { get; init; } = null!;
    public string PaymentStatus { get; init; } = null!;
    public string ProcessingStatus { get; init; } = null!;
    public DateTime? AppointmentDate { get; init; }
    public TimeSpan? AppointmentTime { get; init; }
    public string? VfsReceipt { get; init; }
    public string? PassportCopy { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public System.Collections.Generic.List<GovtFeeDetailResponse> GovtFees { get; init; } = new();
}
