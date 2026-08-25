using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using miniDriveBackend.Models;

namespace miniDriveBackend.Data.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<User>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<User>> GetSuperAdminsAsync(CancellationToken cancellationToken = default);
        Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);
        Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default);
        Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<bool> ExistsByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    }
}