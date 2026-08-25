using System;
using System.Threading;
using System.Threading.Tasks;

namespace miniDriveBackend.Business.Interfaces
{
    public interface IS3Service
    {
        Task<PresignedUploadUrl> GeneratePresignedUploadUrlAsync(string s3Key, string contentType, long contentLength, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
        Task<PresignedDownloadUrl> GeneratePresignedDownloadUrlAsync(string s3Key, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
        Task<PresignedDownloadUrl> GeneratePresignedViewUrlAsync(string s3Key, string contentType, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
        string BuildS3Key(Guid tenantId, Guid userId, string fileName);
        string BuildFolderS3Key(Guid tenantId, string folderPath);
        bool ValidateS3KeyFormat(string s3Key, Guid tenantId);
        Task<bool> ObjectExistsAsync(string s3Key, CancellationToken cancellationToken = default);
        Task DeleteObjectAsync(string s3Key, CancellationToken cancellationToken = default);
    }

    public record PresignedUploadUrl(string UploadUrl, string S3Key, DateTimeOffset ExpiresAt, IDictionary<string, string> RequiredHeaders);

    public record PresignedDownloadUrl(string DownloadUrl, DateTimeOffset ExpiresAt);
}