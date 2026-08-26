using System.Threading;
using System.Threading.Tasks;
using miniDriveBackend.Business.DTOs;

namespace miniDriveBackend.Business.Interfaces
{
    public interface IAnalyticsService
    {
        Task<AnalyticsOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default);
    }
}
