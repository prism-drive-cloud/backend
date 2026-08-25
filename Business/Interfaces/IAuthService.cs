using System;
using System.Threading;
using System.Threading.Tasks;
using miniDriveBackend.Business.DTOs;
using miniDriveBackend.Models;

namespace miniDriveBackend.Business.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
        Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request, CancellationToken cancellationToken = default);
        Task<AuthResponse> RegisterUserAsync(RegisterUserRequest request, Guid currentUserId, CancellationToken cancellationToken = default);
        Task<UserProfileResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default);
        Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
        Task RevokeRefreshTokenAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}