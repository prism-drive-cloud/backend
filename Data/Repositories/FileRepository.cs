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
    public class FileRepository : BaseRepository<FileEntity>, IFileRepository
    {
        public FileRepository(AppDbContext context) : base(context) { }

        public async Task<FileEntity?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Where(f => f.TenantId == tenantId)
                .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<FileEntity>> GetByTenantIdAsync(Guid tenantId, int page, int pageSize, string? searchTerm = null, CancellationToken cancellationToken = default)
        {
            var query = DbSet.Where(f => f.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(f => f.OriginalName.Contains(searchTerm));
            }

            return await query
                .OrderByDescending(f => f.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<FileEntity>> GetByFolderIdAsync(Guid folderId, Guid tenantId, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Where(f => f.TenantId == tenantId && f.FolderId == folderId)
                .OrderBy(f => f.OriginalName)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<FileEntity>> GetRootFilesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Where(f => f.TenantId == tenantId && f.FolderId == null)
                .OrderBy(f => f.OriginalName)
                .ToListAsync(cancellationToken);
        }

        public async Task<long> GetTotalCountByTenantIdAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        {
            var query = DbSet.Where(f => f.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(f => f.OriginalName.Contains(searchTerm));
            }

            return await query.LongCountAsync(cancellationToken);
        }

        public async Task<long> GetTotalSizeByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Where(f => f.TenantId == tenantId)
                .SumAsync(f => f.SizeBytes, cancellationToken);
        }

        public override async Task<FileEntity> CreateAsync(FileEntity file, CancellationToken cancellationToken = default)
        {
            await DbSet.AddAsync(file, cancellationToken);
            await Context.SaveChangesAsync(cancellationToken);
            return file;
        }

        public override async Task<FileEntity> UpdateAsync(FileEntity file, CancellationToken cancellationToken = default)
        {
            DbSet.Update(file);
            await Context.SaveChangesAsync(cancellationToken);
            return file;
        }

        public async Task<bool> SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        {
            var file = await GetByIdAsync(id, tenantId, cancellationToken);
            if (file == null)
                return false;

            file.IsDeleted = true;
            file.DeletedAt = DateTimeOffset.UtcNow;
            await Context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> ExistsByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        {
            return await DbSet.AnyAsync(f => f.Id == id && f.TenantId == tenantId, cancellationToken);
        }

        public async Task<bool> ExistsByS3KeyAsync(string s3Key, CancellationToken cancellationToken = default)
        {
            return await DbSet.AnyAsync(f => f.S3Key == s3Key, cancellationToken);
        }
    }
}