using System.ComponentModel.DataAnnotations;

namespace miniDriveBackend.Models
{
    public class Tenant : BaseEntity
    {
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Slug { get; set; } = string.Empty;

        public bool IsPersonal { get; set; } = false;

        public long StorageQuotaBytes { get; set; } = 1073741824;
    }
}