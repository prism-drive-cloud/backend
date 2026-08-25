using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using miniDriveBackend.Models;

namespace miniDriveBackend.Data.Interfaces
{
    public interface IFolderRepository
    {
        Task<Folder?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Folder>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Folder>> GetRootFoldersAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<Folder> CreateAsync(Folder folder, CancellationToken cancellationToken = default);
        Task<Folder> UpdateAsync(Folder folder, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
        Task<bool> ExistsByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    }
}