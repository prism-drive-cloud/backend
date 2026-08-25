using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using miniDriveBackend.Business.DTOs;
using miniDriveBackend.Models;

namespace miniDriveBackend.Business.Interfaces
{
    public interface IFileService
    {
        Task<PagedResult<FileResponse>> GetFilesAsync(Guid tenantId, FileQueryParameters parameters, CancellationToken cancellationToken = default);
        Task<FileResponse?> GetFileByIdAsync(Guid fileId, Guid tenantId, CancellationToken cancellationToken = default);
        Task<UploadUrlResponse> RequestUploadUrlAsync(Guid tenantId, Guid userId, UploadUrlRequest request, CancellationToken cancellationToken = default);
        Task<FileResponse> ConfirmUploadAsync(Guid tenantId, Guid userId, ConfirmUploadRequest request, CancellationToken cancellationToken = default);
        Task<DownloadUrlResponse> GetDownloadUrlAsync(Guid fileId, Guid tenantId, CancellationToken cancellationToken = default);
        Task<FileResponse> RenameAsync(Guid fileId, Guid tenantId, RenameFileRequest request, CancellationToken cancellationToken = default);
        Task<FileResponse> MoveAsync(Guid fileId, Guid tenantId, MoveFileRequest request, CancellationToken cancellationToken = default);
        Task<bool> SoftDeleteAsync(Guid fileId, Guid tenantId, CancellationToken cancellationToken = default);
        Task<long> GetTotalSizeByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    }
}