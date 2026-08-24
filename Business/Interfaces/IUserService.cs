using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using miniDriveBackend.Business.DTOs;
using miniDriveBackend.Models;

namespace miniDriveBackend.Business.Interfaces
{
    public interface IUserService
    {
        Task<UserResponse> CreateUserAsync(Guid tenantId, CreateUserRequest request, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserResponse>> GetUsersByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<UserResponse?> GetUserByIdAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
        Task<UserResponse> UpdateUserAsync(Guid userId, Guid tenantId, UpdateUserRequest request, CancellationToken cancellationToken = default);
        Task<bool> DeactivateUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
        Task<bool> ActivateUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
        Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<bool> ExistsByIdAndTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
    }
}