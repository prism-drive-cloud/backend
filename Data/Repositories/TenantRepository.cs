using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using miniDriveBackend.Data;
using miniDriveBackend.Data.Interfaces;
using miniDriveBackend.Models;

namespace miniDriveBackend.Data.Repositories
{
    public class TenantRepository : BaseRepository<Tenant>, ITenantRepository
    {
        public TenantRepository(AppDbContext context) : base(context) { }

        public override async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await DbSet.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        }

        public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        {
            return await DbSet.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
        }

        public override async Task<Tenant> CreateAsync(Tenant tenant, CancellationToken cancellationToken = default)
        {
            await DbSet.AddAsync(tenant, cancellationToken);
            await Context.SaveChangesAsync(cancellationToken);
            return tenant;
        }

        public async Task<long> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return await Context.Files
                .Where(f => f.TenantId == tenantId && !f.IsDeleted)
                .SumAsync(f => f.SizeBytes, cancellationToken);
        }

        public async Task<TenantUsageSummary> GetUsageSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var fileStats = await Context.Files
                .Where(f => f.TenantId == tenantId && !f.IsDeleted)
                .GroupBy(_ => 1)
                .Select(g => new { UsedBytes = g.Sum(f => f.SizeBytes), FileCount = g.Count() })
                .FirstOrDefaultAsync(cancellationToken);

            var folderCount = await Context.Folders
                .CountAsync(f => f.TenantId == tenantId, cancellationToken);

            return new TenantUsageSummary(
                fileStats?.UsedBytes ?? 0L,
                fileStats?.FileCount ?? 0,
                folderCount);
        }

        public async Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default)
        {
            return await DbSet.AnyAsync(t => t.Slug == slug, cancellationToken);
        }
    }
}