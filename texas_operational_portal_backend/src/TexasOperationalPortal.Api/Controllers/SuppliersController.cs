using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;

namespace TexasOperationalPortal.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly ISalesOperationsClient _salesClient;

    public SuppliersController(ISalesOperationsClient salesClient)
    {
        _salesClient = salesClient;
    }

    [HttpGet]
    public async Task<IActionResult> GetSuppliers(CancellationToken ct)
    {
        var suppliers = await _salesClient.GetSuppliersAsync(ct);
        return Ok(suppliers);
    }
}
