using System;
using System.Collections.Generic;

namespace TexasOperationalPortal.Contracts.Bookings;

public sealed record VisaApplicationSummaryResponse
{
    public bool OperationClosed { get; init; }
    public bool IsDealClosed { get; init; }
    public long Id { get; init; }
    public string? DocumentNumber { get; init; }
    public string? HeadName { get; init; }
    public string? VisaPackName { get; init; }
    public string? SalesmanName { get; init; }
    public string? CountryName { get; init; }
    public string? Country { get; init; }
    public string? PassportNumber { get; init; }
    public string? PassportNo { get; init; }
    public string? GovtId { get; init; }
    public string? GovtTrackingId { get; init; }
    public string? ApplicantFirstName { get; init; }
    public string? ApplicantFirstNames { get; init; }
    public string? ApplicantNames { get; init; }
    public string ClientType { get; init; } = null!;
    public string ProcessingStatus { get; init; } = null!;
    public string PaymentStatus { get; init; } = null!;
    public decimal PayableAmount { get; init; }
    public decimal Profit { get; init; }
    public int ApplicantCount { get; init; }
    public DateTime? CreatedAt { get; init; }
    public string? VisaDocument { get; init; }
}

public sealed record GovtFeeDetailResponse(
    long Id,
    decimal Amount,
    string? Description,
    string? PaymentReference,
    string? Status,
    string? PaymentProof,
    DateTime? CreatedAt,
    long? SupplierId = null,
    string? SupplierName = null
);

public sealed record VisaApplicationDetailResponse
{
    public bool OperationClosed { get; init; }
    public bool IsDealClosed { get; init; }
    public long Id { get; init; }
    public string? DocumentNumber { get; init; }
    public long UserId { get; init; }
    public int CountryId { get; init; }
    public string? CountryName { get; init; }
    public int VisaPackId { get; init; }
    public string? VisaPackName { get; init; }
    public string ClientType { get; init; } = null!;
    public string? Agent { get; init; }
    public long? AgentId { get; init; }
    public long? HeadId { get; init; }
    public string? HeadName { get; init; }
    public string ProcessingStatus { get; init; } = null!;
    public string PaymentStatus { get; init; } = null!;
    public string? VisaDocument { get; init; }
    public string? ApprovalNote { get; init; }
    public string? RejectionDocument { get; init; }
    public string? RejectionNote { get; init; }
    public string? RefundNote { get; init; }
    public string? DocumentRequestNote { get; init; }
    public DateTime? DocumentRequestDate { get; init; }
    public decimal PayableAmount { get; init; }
    public decimal ExpectedSupplierCost { get; init; }
    public decimal Profit { get; init; }
    public long? SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public decimal RejectionFee { get; init; }
    public string? PaymentMethod { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public List<VisaApplicantResponse> Applicants { get; init; } = new();
    public List<GovtFeeDetailResponse> GovtFees { get; init; } = new();
}

public sealed record VisaApplicantResponse(
    long Id,
    string? Prefix,
    string? FName,
    string? LName,
    DateTime? Dob,
    string? Nationality,
    string? CareOf,
    string? ContactNo,
    string? EmailId,
    string? PassportNo,
    string? IqamaHotelNo,
    decimal PayableAmount,
    decimal ExpectedSupplierCost,
    decimal Profit,
    bool IsInhouse,
    string? PaymentMethod,
    string? PaymentStatus,
    string? PaymentProof,
    string? PaymentReference,
    long? BankId,
    string? BankName,
    string? PassportCopy = null,
    string? BankDoc = null,
    string? GovtFeeReference = null
);


public sealed record UpdateVisaApplicantDetailsRequest(
    string? Prefix,
    string? FName,
    string? LName,
    DateTime? Dob,
    string? Nationality,
    string? PassportNo,
    string? ContactNo,
    string? EmailId
);
