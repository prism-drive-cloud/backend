using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using miniDriveBackend.Data;
using miniDriveBackend.Data.Interfaces;
using miniDriveBackend.Models;

namespace miniDriveBackend.Data.Repositories
{
    public class UserRepository : BaseRepository<User>, IUserRepository
    {
        public UserRepository(AppDbContext context) : base(context) { }

        public override async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await DbSet.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        }

        public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return await DbSet.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        }

        public async Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        {
            return await DbSet.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId, cancellationToken);
        }

        public async Task<IReadOnlyList<User>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Where(u => u.TenantId == tenantId)
                .OrderBy(u => u.FullName)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<User>> GetSuperAdminsAsync(CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Where(u => u.Role == UserRole.SuperAdmin && u.TenantId == null)
                .OrderBy(u => u.FullName)
                .ToListAsync(cancellationToken);
        }

        public override async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
        {
            await DbSet.AddAsync(user, cancellationToken);
            await Context.SaveChangesAsync(cancellationToken);
            return user;
        }

        public override async Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            DbSet.Update(user);
            await Context.SaveChangesAsync(cancellationToken);
            return user;
        }

        public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return await DbSet.AnyAsync(u => u.Email == email, cancellationToken);
        }

        public async Task<bool> ExistsByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        {
            return await DbSet.AnyAsync(u => u.Id == id && u.TenantId == tenantId, cancellationToken);
        }
    }
}