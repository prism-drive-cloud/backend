using System.ComponentModel.DataAnnotations;

namespace miniDriveBackend.Models
{
    public class FileEntity : BaseEntity
    {
        public Guid TenantId { get; set; }

        public Guid OwnerId { get; set; }

        public Guid? FolderId { get; set; }

        [Required]
        [MaxLength(255)]
        public string OriginalName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string MimeType { get; set; } = string.Empty;

        public long SizeBytes { get; set; }

        [Required]
        [MaxLength(500)]
        public string S3Key { get; set; } = string.Empty;

        public bool IsDeleted { get; set; } = false;

        public DateTimeOffset? DeletedAt { get; set; }
    }
}