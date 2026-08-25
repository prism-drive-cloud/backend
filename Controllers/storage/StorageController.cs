using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using miniDriveBackend.Api.Extensions;
using miniDriveBackend.Business.Interfaces;

namespace miniDriveBackend.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/storage")]
    public class StorageController : ControllerBase
    {
        private readonly IStorageService _storageService;

        public StorageController(IStorageService storageService)
        {
            _storageService = storageService;
        }

        /// <summary>Bytes usados actualmente por el tenant.</summary>
        [HttpGet("usage")]
        public async Task<ActionResult<long>> GetUsage(CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var usage = await _storageService.GetStorageUsageAsync(tenantId, cancellationToken);
            return Ok(usage);
        }

        /// <summary>Cuota total de almacenamiento asignada al tenant.</summary>
        [HttpGet("quota")]
        public async Task<ActionResult<long>> GetQuota(CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var quota = await _storageService.GetStorageQuotaAsync(tenantId, cancellationToken);
            return Ok(quota);
        }

        /// <summary>Resumen completo: usado, cuota, disponible y porcentaje.</summary>
        [HttpGet("info")]
        public async Task<ActionResult<StorageInfo>> GetStorageInfo(CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var info = await _storageService.GetStorageInfoAsync(tenantId, cancellationToken);
            return Ok(info);
        }

        /// <summary>Verifica si hay cupo disponible para un tamaño solicitado (bytes).</summary>
        [HttpGet("check")]
        public async Task<ActionResult<bool>> CheckQuotaAvailable(
            [FromQuery] long requestedBytes,
            CancellationToken cancellationToken)
        {
            var tenantId = User.GetTenantId();
            var available = await _storageService.CheckQuotaAvailableAsync(tenantId, requestedBytes, cancellationToken);
            return Ok(available);
        }
    }
}