using System.ComponentModel.DataAnnotations;

namespace miniDriveBackend.Models
{
    public class User : BaseEntity
    {
        public Guid? TenantId { get; set; }

        [Required]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string FullName { get; set; } = string.Empty;

        public UserRole Role { get; set; } = UserRole.User;

        public bool IsActive { get; set; } = true;
    }
}