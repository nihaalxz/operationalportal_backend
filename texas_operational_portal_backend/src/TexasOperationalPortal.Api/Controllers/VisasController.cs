using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Application.Features.Visas;
using TexasOperationalPortal.Contracts.Common;
using TexasOperationalPortal.Contracts.Bookings;

namespace TexasOperationalPortal.Api.Controllers;

[ApiController]
[Route("api/visas")]
[Authorize(Policy = "CanViewVisas")]
[Produces("application/json")]
public sealed class VisasController : ControllerBase
{
    private readonly GetVisaQueueUseCase _getQueueUseCase;
    private readonly GetVisaDetailsUseCase _getDetailsUseCase;
    private readonly ISalesOperationsClient _salesClient;
    private readonly TexasOperationalPortal.Application.Features.Assignments.IAssignmentService _assignmentService;

    public VisasController(
        GetVisaQueueUseCase getQueueUseCase,
        GetVisaDetailsUseCase getDetailsUseCase,
        ISalesOperationsClient salesClient,
        TexasOperationalPortal.Application.Features.Assignments.IAssignmentService assignmentService)
    {
        _getQueueUseCase = getQueueUseCase;
        _getDetailsUseCase = getDetailsUseCase;
        _salesClient = salesClient;
        _assignmentService = assignmentService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<VisaApplicationSummaryResponse>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetQueue(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null, [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) return BadRequest("Invalid paging parameters.");

        var result = await _getQueueUseCase.ExecuteAsync(page, pageSize, status, search, ct);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(VisaApplicationDetailResponse), 200)]
    [ProducesResponseType(401), ProducesResponseType(404)]
    public async Task<IActionResult> GetDetails([FromRoute] long id, CancellationToken ct)
    {
        var result = await _getDetailsUseCase.ExecuteAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:long}/process")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ProcessVisa([FromRoute] long id, CancellationToken ct)
    {
        var form = HttpContext.Request.Form;
        var govAppNo = form["gov_application_no"].ToString();
        if (string.IsNullOrWhiteSpace(govAppNo)) return BadRequest("gov_application_no is required.");

        var details = await _getDetailsUseCase.ExecuteAsync(id, ct);
        if (details == null || details.Applicants.Count == 0)
            return NotFound("Visa application or applicants not found.");

        var firstApplicantId = details.Applicants[0].Id;
        var govtFees = new List<KeyValuePair<string, string>>();
        var govtFeeProofs = new List<FileStreamInput>();

        foreach (var key in form.Keys)
        {
            if (key.StartsWith("govt_fees["))
            {
                govtFees.Add(new KeyValuePair<string, string>(key, form[key].ToString()));
            }
        }

        foreach (var file in form.Files)
        {
            if (file.Name.StartsWith("govt_fees["))
            {
                var mapped = MapFile(file);
                if (mapped != null) govtFeeProofs.Add(mapped);
            }
        }

        try
        {
            await _assignmentService.ClaimOperationAsync(
                new TexasOperationalPortal.Contracts.Assignments.ClaimAssignmentRequest("Visa", id, details.DocumentNumber, "Auto-claimed on processing"),
                GetCurrentUserId(), ct);
        }
        catch (TexasOperationalPortal.Application.Common.Exceptions.AssignmentConflictException)
        {
            // Ignore if already assigned to operator
        }

        var isAdditive = form["is_additive"].ToString().Equals("true", System.StringComparison.OrdinalIgnoreCase) ||
                         form["isAdditive"].ToString().Equals("true", System.StringComparison.OrdinalIgnoreCase);

        var success = await _salesClient.UpdateVisaIdAsync(
            firstApplicantId, govAppNo, null, null, null, govtFees, govtFeeProofs, GetCurrentUserId(), isAdditive, ct);

        return success ? Ok() : StatusCode(500, "Failed to update visa processing state on sales service.");
    }

    [HttpPost("{id:long}/update-gov-ref")]
    public async Task<IActionResult> UpdateGovRef([FromRoute] long id, [FromBody] Dictionary<string, string> body, CancellationToken ct)
    {
        if (!body.TryGetValue("gov_application_no", out var govAppNo) || string.IsNullOrWhiteSpace(govAppNo))
            return BadRequest("gov_application_no is required.");

        var details = await _getDetailsUseCase.ExecuteAsync(id, ct);
        if (details == null || details.Applicants.Count == 0)
            return NotFound("Visa application not found.");

        var firstApplicantId = details.Applicants[0].Id;
        var success = await _salesClient.EditVisaIdAsync(firstApplicantId, govAppNo, GetCurrentUserId(), ct);

        return success ? Ok() : StatusCode(500, "Failed to update gov reference on sales service.");
    }

    [HttpPost("{id:long}/approve")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ApproveVisa([FromRoute] long id, [FromForm] IFormFile? visa_document, [FromForm] string? desc_note, CancellationToken ct)
    {
        var doc = visa_document != null ? MapFile(visa_document) : null;
        var success = await _salesClient.ApproveVisaAsync(id, doc, desc_note, GetCurrentUserId(), ct);
        if (success)
        {
            await _assignmentService.CompleteOperationAsync("Visa", id, GetCurrentUserId(), ct);
            return Ok();
        }
        return StatusCode(500, "Failed to approve visa on sales service.");
    }

    [HttpPost("{id:long}/reject")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> RejectVisa(
        [FromRoute] long id,
        [FromForm] IFormFile? rejection_document,
        [FromForm] IFormFile? rejectionDocument,
        [FromForm] string? desc_note,
        [FromForm] string? descNote,
        [FromForm] string? refund_note,
        [FromForm] string? refundNote,
        [FromForm] decimal? rejection_fee,
        [FromForm] decimal? rejectionFee,
        [FromForm] decimal? payable_amount,
        [FromForm] decimal? payableAmount,
        CancellationToken ct)
    {
        var finalPayableAmount = payable_amount ?? payableAmount;
        var finalRejectionFee = rejectionFee ?? rejection_fee ?? 0m;
        var fileToMap = rejection_document ?? rejectionDocument;
        var doc = fileToMap != null ? MapFile(fileToMap) : null;
        var rawNote = !string.IsNullOrWhiteSpace(desc_note) ? desc_note : descNote;
        var note = !string.IsNullOrWhiteSpace(rawNote) ? rawNote : "Rejected by operational staff";
        var finalRefundNote = !string.IsNullOrWhiteSpace(refund_note) ? refund_note : refundNote;

        var success = await _salesClient.RejectVisaAsync(
            id, doc, note, finalRefundNote, finalRejectionFee, finalPayableAmount, GetCurrentUserId(), ct);

        if (success)
        {
            await _assignmentService.CompleteOperationAsync("Visa", id, GetCurrentUserId(), ct);
            return Ok();
        }
        return StatusCode(500, "Failed to reject visa on sales service.");
    }

    public record RequestDocUpdateBody(string? request_note, string? requestNote);

    [HttpPost("{id:long}/request-document-update")]
    public async Task<IActionResult> RequestDocumentUpdate([FromRoute] long id, [FromBody] RequestDocUpdateBody body, CancellationToken ct)
    {
        var note = !string.IsNullOrWhiteSpace(body.request_note) ? body.request_note : body.requestNote;
        if (string.IsNullOrWhiteSpace(note)) return BadRequest("request_note is required.");

        var success = await _salesClient.RequestVisaDocumentUpdateAsync(id, note, GetCurrentUserId(), ct);
        return success ? Ok() : StatusCode(500, "Failed to request document update on sales service.");
    }

    public class UpdatePayableAmountDto
    {
        public decimal? PayableAmount { get; set; }
        public decimal? Payable_Amount { get; set; }
    }

    [HttpPut("{id:long}/payable-amount")]
    [HttpPost("{id:long}/payable-amount")]
    public async Task<IActionResult> UpdatePayableAmount([FromRoute] long id, [FromBody] UpdatePayableAmountDto dto, CancellationToken ct)
    {
        var amount = dto?.PayableAmount ?? dto?.Payable_Amount;
        if (!amount.HasValue || amount.Value < 0) return BadRequest("payable_amount cannot be negative.");

        var success = await _salesClient.UpdateVisaPayableAmountAsync(id, amount.Value, ct);
        if (success) return Ok(new { Message = "Payable amount updated successfully." });
        return StatusCode(500, "Failed to update payable amount on sales service.");
    }

    private long GetCurrentUserId()
    {
        var subClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value
                       ?? User.FindFirst("id")?.Value
                       ?? User.FindFirst("userId")?.Value
                       ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

        return long.TryParse(subClaim, out var userId) ? userId : 0;
    }

    private static FileStreamInput? MapFile(IFormFile? file)
    {
        if (file == null || file.Length == 0) return null;
        return new FileStreamInput
        {
            Stream = file.OpenReadStream(),
            FileName = file.FileName,
            ContentType = file.ContentType
        };
    }

    [HttpPost("{id:long}/close")]
    public async Task<IActionResult> CloseVisa([FromRoute] long id, CancellationToken ct)
    {
        var success = await _salesClient.CloseVisaOperationAsync(id, ct);
        if (!success) return StatusCode(500, "Failed to close visa operation on sales service.");

        try
        {
            await _assignmentService.CompleteOperationAsync("Visa", id, GetCurrentUserId(), ct);
        }
        catch { }

        return Ok(new { message = "Visa operation closed successfully." });
    }

    [HttpPut("applicants/{applicantId:long}")]
    public async Task<IActionResult> UpdateApplicantDetails([FromRoute] long applicantId, [FromBody] TexasOperationalPortal.Contracts.Bookings.UpdateVisaApplicantDetailsRequest request, CancellationToken ct)
    {
        var success = await _salesClient.UpdateApplicantDetailsAsync(applicantId, request, ct);
        return success ? Ok() : StatusCode(500, "Failed to update applicant details on sales service.");
    }

}