using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Application.Features.Vfs;
using TexasOperationalPortal.Contracts.Common;
using TexasOperationalPortal.Contracts.Bookings;

namespace TexasOperationalPortal.Api.Controllers;

[ApiController]
[Route("api/vfs-appointments")]
[Authorize(Policy = "CanViewVfs")]
[Produces("application/json")]
public sealed class VfsController : ControllerBase
{
    private readonly GetVfsQueueUseCase _getQueueUseCase;
    private readonly GetVfsDetailsUseCase _getDetailsUseCase;
    private readonly ISalesOperationsClient _salesClient;
    private readonly TexasOperationalPortal.Application.Features.Assignments.IAssignmentService _assignmentService;

    public VfsController(
        GetVfsQueueUseCase getQueueUseCase,
        GetVfsDetailsUseCase getDetailsUseCase,
        ISalesOperationsClient salesClient,
        TexasOperationalPortal.Application.Features.Assignments.IAssignmentService assignmentService)
    {
        _getQueueUseCase = getQueueUseCase;
        _getDetailsUseCase = getDetailsUseCase;
        _salesClient = salesClient;
        _assignmentService = assignmentService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<VfsAppointmentSummaryResponse>), 200)]
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
    [ProducesResponseType(typeof(VfsAppointmentDetailResponse), 200)]
    [ProducesResponseType(401), ProducesResponseType(404)]
    public async Task<IActionResult> GetDetails([FromRoute] long id, CancellationToken ct)
    {
        var result = await _getDetailsUseCase.ExecuteAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:long}/process")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ProcessVfs([FromRoute] long id, CancellationToken ct)
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
                new TexasOperationalPortal.Contracts.Assignments.ClaimAssignmentRequest("VFS", id, details?.DocumentNumber, "Auto-claimed on processing"),
                GetCurrentUserId(), ct);
        }
        catch (TexasOperationalPortal.Application.Common.Exceptions.AssignmentConflictException)
        {
            // Ignore if already assigned to operator
        }

        var isAdditive = form["is_additive"].ToString().Equals("true", System.StringComparison.OrdinalIgnoreCase) ||
                         form["isAdditive"].ToString().Equals("true", System.StringComparison.OrdinalIgnoreCase);

        var success = await _salesClient.ProcessVfsAsync(
            id, govtFees, govtFeeProofs, GetCurrentUserId(), isAdditive, ct);

        return success ? Ok() : StatusCode(500, "Failed to process VFS appointment on sales service.");
    }

    [HttpPost("{id:long}/update")]
    public async Task<IActionResult> UpdateVfsSchedule([FromRoute] long id, [FromBody] Dictionary<string, string> body, CancellationToken ct)
    {
        if (!body.TryGetValue("appointment_date", out var appDateStr) || !System.DateTime.TryParse(appDateStr, out var appDate))
            return BadRequest("appointment_date is required and must be valid.");

        if (!body.TryGetValue("appointment_time", out var appTime) || string.IsNullOrWhiteSpace(appTime))
            return BadRequest("appointment_time is required.");

        body.TryGetValue("description", out var description);

        var success = await _salesClient.UpdateVfsScheduleAsync(
            id, appDate, appTime, description, GetCurrentUserId(), ct);

        return success ? Ok() : StatusCode(500, "Failed to update VFS schedule on sales service.");
    }

    [HttpPost("{id:long}/update-vfs-ref")]
    public async Task<IActionResult> UpdateVfsRef([FromRoute] long id, [FromBody] Dictionary<string, string> body, CancellationToken ct)
    {
        if (!body.TryGetValue("vfs_app_id", out var vfsAppId) || string.IsNullOrWhiteSpace(vfsAppId))
            return BadRequest("vfs_app_id is required.");

        var success = await _salesClient.UpdateVfsRefAsync(id, vfsAppId, GetCurrentUserId(), ct);
        return success ? Ok() : StatusCode(500, "Failed to update VFS tracking ref on sales service.");
    }

    [HttpPost("{id:long}/status")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateVfsStatus([FromRoute] long id, CancellationToken ct)
    {
        var form = HttpContext.Request.Form;
        var status = form["status"].ToString();
        if (string.IsNullOrWhiteSpace(status)) return BadRequest("status is required.");

        IFormFile? vfsDoc = form.Files["vfsDoc"];
        IFormFile? verifiedReceipt = form.Files["verifiedReceipt"];
        IFormFile? rejectedReceipt = form.Files["rejectedReceipt"];

        if (status.ToLower() == "completed")
        {
            if (vfsDoc == null) return BadRequest("vfsDoc is required for completed status.");
            if (verifiedReceipt == null) return BadRequest("verifiedReceipt is required for completed status.");
        }

        var success = await _salesClient.UpdateVfsStatusAsync(
            id, status, MapFile(vfsDoc), MapFile(verifiedReceipt), MapFile(rejectedReceipt), GetCurrentUserId(), ct);

        if (success)
        {
            var st = status.ToLower();
            if (st == "completed" || st == "rejected" || st == "cancelled")
            {
                await _assignmentService.CompleteOperationAsync("VFS", id, GetCurrentUserId(), ct);
            }
            return Ok();
        }

        return StatusCode(500, "Failed to update VFS status on sales service.");
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
