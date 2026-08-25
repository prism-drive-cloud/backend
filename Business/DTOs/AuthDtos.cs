using System.ComponentModel.DataAnnotations;
using miniDriveBackend.Models;

namespace miniDriveBackend.Business.DTOs
{
    public record LoginRequest(
        [Required][EmailAddress] string Email,
        [Required] string Password
    );

    public record RegisterTenantRequest(
        [Required][MaxLength(255)] string TenantName,
        [Required][MaxLength(100)] string Slug,
        [Required][EmailAddress] string AdminEmail,
        [Required][MinLength(8)] string AdminPassword,
        [Required][MaxLength(255)] string AdminFullName
    );

    public record RegisterUserRequest(
        [Required][EmailAddress] string Email,
        [Required][MinLength(8)] string Password,
        [Required][MaxLength(255)] string FullName,
        UserRole Role = UserRole.User
    );

    public record AuthResponse(
        string AccessToken,
        string RefreshToken,
        DateTimeOffset ExpiresAt,
        UserProfileResponse User,
        TenantResponse? Tenant
    );

    public record UserProfileResponse(
        Guid Id,
        string Email,
        string FullName,
        UserRole Role,
        bool IsActive,
        DateTimeOffset CreatedAt
    );

    public record TokenRefreshRequest(
        [Required] string RefreshToken
    );

    public record ChangePasswordRequest(
        [Required] string CurrentPassword,
        [Required][MinLength(8)] string NewPassword
    );
}