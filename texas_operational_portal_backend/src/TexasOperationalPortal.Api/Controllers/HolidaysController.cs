using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Application.Features.Holidays;
using TexasOperationalPortal.Contracts.Common;
using TexasOperationalPortal.Contracts.Bookings;

namespace TexasOperationalPortal.Api.Controllers;

[ApiController]
[Route("api/holiday-packages")]
[Authorize(Policy = "CanViewHolidays")]
[Produces("application/json")]
public sealed class HolidaysController : ControllerBase
{
    private readonly GetHolidayQueueUseCase _getQueueUseCase;
    private readonly GetHolidayDetailsUseCase _getDetailsUseCase;
    private readonly ISalesOperationsClient _salesClient;
    private readonly TexasOperationalPortal.Application.Features.Assignments.IAssignmentService _assignmentService;

    public HolidaysController(
        GetHolidayQueueUseCase getQueueUseCase,
        GetHolidayDetailsUseCase getDetailsUseCase,
        ISalesOperationsClient salesClient,
        TexasOperationalPortal.Application.Features.Assignments.IAssignmentService assignmentService)
    {
        _getQueueUseCase = getQueueUseCase;
        _getDetailsUseCase = getDetailsUseCase;
        _salesClient = salesClient;
        _assignmentService = assignmentService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<HolidayPackageSummaryResponse>), 200)]
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
    [ProducesResponseType(typeof(HolidayPackageDetailResponse), 200)]
    [ProducesResponseType(401), ProducesResponseType(404)]
    public async Task<IActionResult> GetDetails([FromRoute] long id, CancellationToken ct)
    {
        var result = await _getDetailsUseCase.ExecuteAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:long}/process")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ProcessHoliday([FromRoute] long id, CancellationToken ct)
    {
        var form = HttpContext.Request.Form;
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
            var details = await _getDetailsUseCase.ExecuteAsync(id, ct);
            await _assignmentService.ClaimOperationAsync(
                new TexasOperationalPortal.Contracts.Assignments.ClaimAssignmentRequest("Holiday", id, details?.DocumentNumber, "Auto-claimed on processing"),
                GetCurrentUserId(), ct);
        }
        catch (TexasOperationalPortal.Application.Common.Exceptions.AssignmentConflictException)
        {
            // Ignore if already assigned to operator
        }

        var success = await _salesClient.ProcessHolidayAsync(
            id, govtFees, govtFeeProofs, GetCurrentUserId(), ct);

        return success ? Ok() : StatusCode(500, "Failed to process holiday package on sales service.");
    }

    [HttpPost("{id:long}/status")]
    public async Task<IActionResult> UpdateHolidayStatus([FromRoute] long id, [FromBody] Dictionary<string, string> body, CancellationToken ct)
    {
        if (!body.TryGetValue("status", out var status) || string.IsNullOrWhiteSpace(status))
            return BadRequest("status is required.");

        var success = await _salesClient.UpdateHolidayStatusAsync(
            id, status, GetCurrentUserId(), ct);

        if (success)
        {
            var st = status.ToLower();
            if (st == "completed" || st == "rejected" || st == "cancelled")
            {
                await _assignmentService.CompleteOperationAsync("Holiday", id, GetCurrentUserId(), ct);
            }
            return Ok();
        }

        return StatusCode(500, "Failed to update holiday status on sales service.");
    }

    [HttpPost("{id:long}/update-holiday-id")]
    public async Task<IActionResult> UpdateHolidayRef([FromRoute] long id, [FromBody] Dictionary<string, string> body, CancellationToken ct)
    {
        var success = await _salesClient.UpdateHolidayRefAsync(id, GetCurrentUserId(), ct);
        return success ? Ok() : StatusCode(500, "Failed to update holiday supplier ref on sales service.");
    }

    [HttpPost("{id:long}/documents")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadHolidayDocs([FromRoute] long id, CancellationToken ct)
    {
        var form = HttpContext.Request.Form;
        var documents = new List<KeyValuePair<string, FileStreamInput>>();

        // Read documents[X][serviceName] and documents[X][file] from multipart form
        var tempDocNames = new Dictionary<string, string>();
        foreach (var key in form.Keys)
        {
            var match = System.Text.RegularExpressions.Regex.Match(key, @"(?:documents|Documents)\[(\d+)\](?:\[|\.)(?:serviceName|ServiceName|service_name)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var index = match.Groups[1].Value;
                tempDocNames[index] = form[key].ToString();
            }
        }

        foreach (var file in form.Files)
        {
            var match = System.Text.RegularExpressions.Regex.Match(file.Name, @"(?:documents|Documents)\[(\d+)\](?:\[|\.)(?:file|File)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var index = match.Groups[1].Value;
                var serviceName = tempDocNames.TryGetValue(index, out var name) ? name : "Travel Document";
                var mapped = MapFile(file);
                if (mapped != null)
                {
                    documents.Add(new KeyValuePair<string, FileStreamInput>(serviceName, mapped));
                }
            }
        }

        if (documents.Count == 0) return BadRequest("At least one document is required.");

        var success = await _salesClient.UploadHolidayDocsAsync(id, documents, ct);
        return success ? Ok() : StatusCode(500, "Failed to upload holiday documents on sales service.");
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
}
