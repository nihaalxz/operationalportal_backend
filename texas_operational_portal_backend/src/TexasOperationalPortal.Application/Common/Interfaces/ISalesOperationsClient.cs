using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Contracts.Common;
using TexasOperationalPortal.Contracts.Bookings;
using TexasOperationalPortal.Contracts.Dashboard;

namespace TexasOperationalPortal.Application.Common.Interfaces;

public class FileStreamInput
{
    public Stream Stream { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
}

public interface ISalesOperationsClient
{
    /// <summary>
    /// Returns accurate pending and under-review counts for Visas, VFS, and Holidays
    /// in a single HTTP call — more reliable and efficient than 6 separate paginated calls.
    /// </summary>
    Task<OperationalStatusCountsResponse> GetOperationalStatusCountsAsync(CancellationToken ct);

    Task<PagedResponse<VisaApplicationSummaryResponse>> GetVisaApplicationsAsync(
        int page, int pageSize, string? status, string? search, CancellationToken ct);

    Task<VisaApplicationDetailResponse?> GetVisaApplicationDetailsAsync(
        long id, CancellationToken ct);

    Task<PagedResponse<VfsAppointmentSummaryResponse>> GetVfsAppointmentsAsync(
        int page, int pageSize, string? status, string? search, CancellationToken ct);

    Task<VfsAppointmentDetailResponse?> GetVfsAppointmentDetailsAsync(
        long id, CancellationToken ct);

    Task<PagedResponse<HolidayPackageSummaryResponse>> GetHolidayPackagesAsync(
        int page, int pageSize, string? status, string? search, CancellationToken ct);

    Task<HolidayPackageDetailResponse?> GetHolidayPackageDetailsAsync(
        long id, CancellationToken ct);

    // ─── Visa Actions ───
    Task<bool> UpdateVisaIdAsync(long firstApplicantId, string visaAppId, decimal? govtFeeAmount, string? govtFeeTransactionNo, FileStreamInput? govtFeePaymentProof, List<KeyValuePair<string, string>> govtFees, List<FileStreamInput> govtFeeProofs, long processedBy, bool isAdditive = false, CancellationToken ct = default);
    Task<bool> EditVisaIdAsync(long firstApplicantId, string visaAppId, long processedBy, CancellationToken ct);
    Task<bool> ApproveVisaAsync(long id, FileStreamInput? visaDoc, string? note, long processedBy, CancellationToken ct);
    Task<bool> RejectVisaAsync(long id, FileStreamInput? rejectionDoc, string note, string? refundNote, decimal rejectionFee, decimal? payableAmount, long processedBy, CancellationToken ct);
    Task<bool> RequestVisaDocumentUpdateAsync(long id, string note, long processedBy, CancellationToken ct);
    Task<bool> UpdateVisaPayableAmountAsync(long id, decimal payableAmount, CancellationToken ct);
    Task<bool> UpdateApplicantDetailsAsync(long applicantId, UpdateVisaApplicantDetailsRequest request, CancellationToken ct);

    // ─── VFS Actions ───
    Task<bool> ProcessVfsAsync(long id, List<KeyValuePair<string, string>> govtFees, List<FileStreamInput> govtFeeProofs, long processedBy, bool isAdditive = false, CancellationToken ct = default);
    Task<bool> UpdateVfsScheduleAsync(long id, System.DateTime appointmentDate, string appointmentTime, string? description, long processedBy, CancellationToken ct);
    Task<bool> UpdateVfsRefAsync(long id, string vfsAppId, long processedBy, CancellationToken ct);
    Task<bool> UpdateVfsStatusAsync(long id, string status, FileStreamInput? vfsDoc, FileStreamInput? verifiedReceipt, FileStreamInput? rejectedReceipt, long processedBy, CancellationToken ct);

    // ─── Holiday Actions ───
    Task<bool> ProcessHolidayAsync(long id, List<KeyValuePair<string, string>> govtFees, List<FileStreamInput> govtFeeProofs, long processedBy, CancellationToken ct);
    Task<bool> UpdateHolidayStatusAsync(long id, string status, long processedBy, CancellationToken ct);
    Task<bool> UpdateHolidayRefAsync(long id, long processedBy, CancellationToken ct);
    Task<bool> UploadHolidayDocsAsync(long id, List<KeyValuePair<string, FileStreamInput>> documents, CancellationToken ct);

    // ─── Suppliers ───
    Task<List<SupplierResponse>> GetSuppliersAsync(CancellationToken ct);
    Task<bool> CloseVisaOperationAsync(long id, CancellationToken ct = default);
}