using System.ComponentModel.DataAnnotations;

namespace miniDriveBackend.Business.DTOs
{
    public record FileQueryParameters(
        int Page = 1,
        int PageSize = 20,
        string? SearchTerm = null,
        Guid? FolderId = null,
        string? MimeType = null,
        string SortBy = "CreatedAt",
        string SortOrder = "desc"
    );

    public record FileResponse(
        Guid Id,
        Guid TenantId,
        Guid OwnerId,
        Guid? FolderId,
        string OriginalName,
        string MimeType,
        long SizeBytes,
        string S3Key,
        bool IsDeleted,
        DateTimeOffset? DeletedAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt
    );

    public record UploadUrlRequest(
        [Required][MaxLength(255)] string FileName,
        [Required][MaxLength(100)] string MimeType,
        [Required][Range(1, long.MaxValue)] long SizeBytes,
        Guid? FolderId = null
    );

    public record UploadUrlResponse(
        string UploadUrl,
        string S3Key,
        DateTimeOffset ExpiresAt,
        IDictionary<string, string> RequiredHeaders
    );

    public record ConfirmUploadRequest(
        [Required] string S3Key,
        [Required][MaxLength(255)] string OriginalName,
        [Required][MaxLength(100)] string MimeType,
        [Required][Range(1, long.MaxValue)] long SizeBytes,
        Guid? FolderId = null
    );

    public record DownloadUrlResponse(
        string DownloadUrl,
        DateTimeOffset ExpiresAt,
        string FileName,
        string MimeType,
        long SizeBytes
    );

    public record RenameFileRequest(
        [Required][MaxLength(255)] string NewName
    );

    public record MoveFileRequest(
        Guid? TargetFolderId
    );

    public record PagedResult<T>(
        IReadOnlyList<T> Items,
        long TotalCount,
        int Page,
        int PageSize,
        int TotalPages
    );
}