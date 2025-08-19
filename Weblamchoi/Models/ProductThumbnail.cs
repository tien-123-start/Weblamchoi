using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace weblamchoi.Models
{
    // Lớp mới để lưu ảnh nhỏ
    public class ProductThumbnail
    {
        [Key]
        public int ThumbnailID { get; set; }

        [Required]
        public int ProductID { get; set; }

        [Required]
        [StringLength(500)]
        public string ThumbnailURL { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        [ForeignKey("ProductID")]
        public Product Product { get; set; }
    }
}