using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using miniDriveBackend.Business.DTOs;
using miniDriveBackend.Business.Interfaces;

namespace miniDriveBackend.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/tenants")]
    public class TenantsController : ControllerBase
    {
        private readonly ITenantService _tenantService;

        public TenantsController(ITenantService tenantService)
        {
            _tenantService = tenantService;
        }

        [HttpGet("usage")]
        [ProducesResponseType(typeof(TenantUsageResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<TenantUsageResponse>> GetUsageAsync(CancellationToken cancellationToken)
        {
            if (!TryGetTenantId(out var tenantId))
                return Unauthorized();

            var response = await _tenantService.GetUsageAsync(tenantId, cancellationToken);
            return Ok(response);
        }

        private bool TryGetTenantId(out Guid tenantId)
        {
            return Guid.TryParse(User.FindFirstValue("tenant_id"), out tenantId);
        }
    }
}