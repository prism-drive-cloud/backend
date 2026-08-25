using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using miniDriveBackend.Models;

namespace miniDriveBackend.Data.Interfaces
{
    public interface ITenantRepository
    {
        Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
        Task<Tenant> CreateAsync(Tenant tenant, CancellationToken cancellationToken = default);
        Task<long> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default);
    }
}