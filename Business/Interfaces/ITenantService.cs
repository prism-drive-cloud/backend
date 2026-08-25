using System;
using System.Threading;
using System.Threading.Tasks;
using miniDriveBackend.Business.DTOs;
using miniDriveBackend.Models;

namespace miniDriveBackend.Business.Interfaces
{
    public interface ITenantService
    {
        Task<TenantResponse> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);
        Task<TenantResponse> GetTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<TenantResponse> GetTenantBySlugAsync(string slug, CancellationToken cancellationToken = default);
        Task<TenantUsageResponse> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<bool> ValidateQuotaAsync(Guid tenantId, long requestedBytes, CancellationToken cancellationToken = default);
        Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default);
    }
}