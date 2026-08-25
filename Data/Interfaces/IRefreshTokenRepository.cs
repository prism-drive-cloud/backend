using System;
using System.Threading;
using System.Threading.Tasks;
using miniDriveBackend.Models;

namespace miniDriveBackend.Data.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken> CreateAsync(RefreshToken token, CancellationToken cancellationToken = default);
        Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
        Task<RefreshToken> UpdateAsync(RefreshToken token, CancellationToken cancellationToken = default);
        Task RevokeAllActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
