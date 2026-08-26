using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using miniDriveBackend.Business.DTOs;
using miniDriveBackend.Business.Interfaces;
using miniDriveBackend.Data;

namespace miniDriveBackend.Business.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly AppDbContext _dbContext;

        public AnalyticsService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<AnalyticsOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default)
        {
            var fileTypeStats = await _dbContext.Files
                .GroupBy(file => file.MimeType)
                .Select(group => new AnalyticsFileTypeResponse(
                    group.Key,
                    group.Count(),
                    group.Sum(file => file.SizeBytes)))
                .OrderByDescending(stat => stat.TotalBytes)
                .ToListAsync(cancellationToken);

            var totalStorageBytes = fileTypeStats.Sum(stat => stat.TotalBytes);
            var totalFiles = fileTypeStats.Sum(stat => stat.FileCount);

            return new AnalyticsOverviewResponse(
                totalStorageBytes,
                await _dbContext.Tenants.CountAsync(cancellationToken),
                await _dbContext.Users.CountAsync(cancellationToken),
                totalFiles,
                fileTypeStats);
        }
    }
}
