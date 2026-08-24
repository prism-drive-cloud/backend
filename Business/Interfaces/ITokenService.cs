using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using miniDriveBackend.Models;

namespace miniDriveBackend.Business.Interfaces
{
    public interface ITokenService
    {
        string GenerateAccessToken(User user);
        string GenerateRefreshToken();
        Task<TokenValidationResult> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
        ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
    }

    public record TokenValidationResult(bool IsValid, Guid? UserId, Guid? TenantId, UserRole? Role, string? ErrorMessage);

    public record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
}