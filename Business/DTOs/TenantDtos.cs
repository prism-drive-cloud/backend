using System.ComponentModel.DataAnnotations;

namespace miniDriveBackend.Business.DTOs
{
    public record CreateTenantRequest(
        [Required][MaxLength(255)] string Name,
        [Required][MaxLength(100)] string Slug,
        bool IsPersonal = false,
        long StorageQuotaBytes = 1073741824
    );

    public record TenantResponse(
        Guid Id,
        string Name,
        string Slug,
        bool IsPersonal,
        long StorageQuotaBytes,
        DateTimeOffset CreatedAt
    );

    public record TenantUsageResponse(
        Guid TenantId,
        string TenantName,
        long UsedBytes,
        long QuotaBytes,
        long AvailableBytes,
        double UsagePercentage,
        int FileCount,
        int FolderCount
    );
}