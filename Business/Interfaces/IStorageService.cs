using System;
using System.Threading;
using System.Threading.Tasks;

namespace miniDriveBackend.Business.Interfaces
{
    public interface IStorageService
    {
        Task<bool> CheckQuotaAvailableAsync(Guid tenantId, long requestedBytes, CancellationToken cancellationToken = default);
        Task<long> GetStorageUsageAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<long> GetStorageQuotaAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task ReserveQuotaAsync(Guid tenantId, long bytes, CancellationToken cancellationToken = default);
        Task ReleaseQuotaAsync(Guid tenantId, long bytes, CancellationToken cancellationToken = default);
        Task<StorageInfo> GetStorageInfoAsync(Guid tenantId, CancellationToken cancellationToken = default);
    }

    public record StorageInfo(long UsedBytes, long QuotaBytes, long AvailableBytes, double UsagePercentage);
}