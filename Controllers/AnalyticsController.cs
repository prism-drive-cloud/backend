using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using miniDriveBackend.Business.DTOs;
using miniDriveBackend.Business.Interfaces;
using miniDriveBackend.Models;

namespace miniDriveBackend.Controllers
{
    // TODO: IAnalyticsService and AnalyticsOverviewResponse are not implemented yet.
    // Docs/Business/04-Design-Rationale.md lists this under "Future Extensibility":
    // "Add IAnalyticsService implementing GET /api/v1/analytics/overview".
    // Pending from the Business layer owner: Business/Interfaces/IAnalyticsService.cs
    // (GetOverviewAsync) and Business/DTOs/AnalyticsDtos.cs (AnalyticsOverviewResponse).
    // This controller will not compile until both exist.
    //
    // Expected shape per Docs/requisitos_minimos.md (lines 18, 118):
    // - Super Admin scope: aggregated across ALL tenants (not tenant-scoped like
    //   ITenantService.GetUsageAsync, which is per-tenant).
    // - "Métricas agregadas de almacenamiento, usuarios y tipos de archivo":
    //     storage  -> total bytes used across all tenants, global S3 consumption
    //     usuarios -> total user count (and/or breakdown by role)
    //     tipos de archivo -> breakdown by MIME type / file count
    //   Likely also: total tenant count, since Super Admin "visualiza todas las
    //   empresas registradas".
    [ApiController]
    [Route("api/v1/analytics")]
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        public AnalyticsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        /// <summary>
        /// Returns system-wide aggregated metrics for the Super Admin dashboard:
        /// storage consumption, user counts, and file type breakdown across all tenants.
        /// </summary>
        [HttpGet("overview")]
        [ProducesResponseType(typeof(AnalyticsOverviewResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<AnalyticsOverviewResponse>> GetOverviewAsync(CancellationToken cancellationToken)
        {
            var overview = await _analyticsService.GetOverviewAsync(cancellationToken);
            return Ok(overview);
        }
    }
}
