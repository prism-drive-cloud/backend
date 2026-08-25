using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace miniDriveBackend.Models
{
    public class RefreshToken : BaseEntity
    {
        public Guid UserId { get; set; }

        [Required]
        public string TokenHash { get; set; } = string.Empty;

        public DateTimeOffset ExpiresAt { get; set; }

        public DateTimeOffset? RevokedAt { get; set; }

        public Guid? ReplacedByTokenId { get; set; }

        [NotMapped]
        public bool IsActive => RevokedAt == null && ExpiresAt > DateTimeOffset.UtcNow;
    }
}
