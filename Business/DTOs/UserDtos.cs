using System.ComponentModel.DataAnnotations;
using miniDriveBackend.Models;

namespace miniDriveBackend.Business.DTOs
{
    public record CreateUserRequest(
        [Required][EmailAddress] string Email,
        [Required][MinLength(8)] string Password,
        [Required][MaxLength(255)] string FullName,
        UserRole Role = UserRole.User
    );

    public record UserResponse(
        Guid Id,
        Guid? TenantId,
        string Email,
        string FullName,
        UserRole Role,
        bool IsActive,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt
    );

    public record UpdateUserRequest(
        [MaxLength(255)] string? FullName,
        UserRole? Role,
        bool? IsActive
    );
}