using System.ComponentModel.DataAnnotations;

namespace miniDriveBackend.Models
{
    public class Folder : BaseEntity
    {
        public Guid TenantId { get; set; }

        public Guid OwnerId { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
    }
}