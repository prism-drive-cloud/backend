using System.Collections.Generic;

namespace miniDriveBackend.Business.DTOs
{
    public record AnalyticsOverviewResponse(
        long TotalStorageBytes,
        int TotalTenants,
        int TotalUsers,
        int TotalFiles,
        IReadOnlyList<AnalyticsFileTypeResponse> FilesByType
    );

    public record AnalyticsFileTypeResponse(
        string MimeType,
        int FileCount,
        long TotalBytes
    );
}
