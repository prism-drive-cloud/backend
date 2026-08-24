using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using miniDriveBackend.Models;

namespace miniDriveBackend.Data.Interfaces
{
    public interface IFileRepository
    {
        Task<FileEntity?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<FileEntity>> GetByTenantIdAsync(Guid tenantId, int page, int pageSize, string? searchTerm = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<FileEntity>> GetByFolderIdAsync(Guid folderId, Guid tenantId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<FileEntity>> GetRootFilesAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<long> GetTotalCountByTenantIdAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default);
        Task<long> GetTotalSizeByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<FileEntity> CreateAsync(FileEntity file, CancellationToken cancellationToken = default);
        Task<FileEntity> UpdateAsync(FileEntity file, CancellationToken cancellationToken = default);
        Task<bool> SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
        Task<bool> ExistsByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
        Task<bool> ExistsByS3KeyAsync(string s3Key, CancellationToken cancellationToken = default);
    }
}