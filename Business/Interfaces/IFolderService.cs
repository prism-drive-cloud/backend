using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using miniDriveBackend.Business.DTOs;
using miniDriveBackend.Models;

namespace miniDriveBackend.Business.Interfaces
{
    public interface IFolderService
    {
        Task<FolderResponse> CreateFolderAsync(Guid tenantId, Guid userId, CreateFolderRequest request, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<FolderResponse>> GetFoldersAsync(Guid tenantId, Guid? parentFolderId = null, CancellationToken cancellationToken = default);
        Task<FolderResponse?> GetFolderByIdAsync(Guid folderId, Guid tenantId, CancellationToken cancellationToken = default);
        Task<FolderResponse> RenameFolderAsync(Guid folderId, Guid tenantId, RenameFolderRequest request, CancellationToken cancellationToken = default);
        Task<bool> DeleteFolderAsync(Guid folderId, Guid tenantId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<FolderResponse>> GetRootFoldersAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<FolderResponse>> GetSubFoldersAsync(Guid parentFolderId, Guid tenantId, CancellationToken cancellationToken = default);
    }
}