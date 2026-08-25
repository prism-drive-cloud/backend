using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using miniDriveBackend.Data;
using miniDriveBackend.Data.Interfaces;
using miniDriveBackend.Models;

namespace miniDriveBackend.Data.Repositories
{
    public class RefreshTokenRepository : BaseRepository<RefreshToken>, IRefreshTokenRepository
    {
        public RefreshTokenRepository(AppDbContext context) : base(context) { }

        public override async Task<RefreshToken> CreateAsync(RefreshToken token, CancellationToken cancellationToken = default)
        {
            await DbSet.AddAsync(token, cancellationToken);
            await Context.SaveChangesAsync(cancellationToken);
            return token;
        }

        public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            return await DbSet.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
        }

        public override async Task<RefreshToken> UpdateAsync(RefreshToken token, CancellationToken cancellationToken = default)
        {
            DbSet.Update(token);
            await Context.SaveChangesAsync(cancellationToken);
            return token;
        }

        public async Task RevokeAllActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow;
            var activeTokens = await DbSet
                .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
                .ToListAsync(cancellationToken);

            if (activeTokens.Count == 0)
                return;

            foreach (var token in activeTokens)
            {
                token.RevokedAt = now;
            }

            await Context.SaveChangesAsync(cancellationToken);
        }
    }
}
