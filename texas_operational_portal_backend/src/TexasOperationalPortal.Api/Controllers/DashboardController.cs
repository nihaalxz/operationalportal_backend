using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Features.Dashboard;
using TexasOperationalPortal.Contracts.Dashboard;

namespace TexasOperationalPortal.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = "OperationalPortalAccess")]
[Produces("application/json")]
public sealed class DashboardController : ControllerBase
{
    private readonly GetOperationalDashboardUseCase _getDashboardUseCase;

    public DashboardController(GetOperationalDashboardUseCase getDashboardUseCase)
    {
        _getDashboardUseCase = getDashboardUseCase;
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _getDashboardUseCase.ExecuteAsync(userId, ct);
        return Ok(result);
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
}
