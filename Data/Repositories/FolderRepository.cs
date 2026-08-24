using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using miniDriveBackend.Data;
using miniDriveBackend.Data.Interfaces;
using miniDriveBackend.Models;

namespace miniDriveBackend.Data.Repositories
{
    public class FolderRepository : BaseRepository<Folder>, IFolderRepository
    {
        public FolderRepository(AppDbContext context) : base(context) { }

        public async Task<Folder?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        {
            return await DbSet.FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId, cancellationToken);
        }

        public async Task<IReadOnlyList<Folder>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Where(f => f.TenantId == tenantId)
                .OrderBy(f => f.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Folder>> GetRootFoldersAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return await GetByTenantIdAsync(tenantId, cancellationToken);
        }

        public override async Task<Folder> CreateAsync(Folder folder, CancellationToken cancellationToken = default)
        {
            await DbSet.AddAsync(folder, cancellationToken);
            await Context.SaveChangesAsync(cancellationToken);
            return folder;
        }

        public override async Task<Folder> UpdateAsync(Folder folder, CancellationToken cancellationToken = default)
        {
            DbSet.Update(folder);
            await Context.SaveChangesAsync(cancellationToken);
            return folder;
        }

        public async Task<bool> DeleteAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        {
            var folder = await GetByIdAsync(id, tenantId, cancellationToken);
            if (folder == null)
                return false;

            DbSet.Remove(folder);
            await Context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> ExistsByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        {
            return await DbSet.AnyAsync(f => f.Id == id && f.TenantId == tenantId, cancellationToken);
        }
    }
}