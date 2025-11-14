using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace weblamchoi.Models
{
    [Table("MomoResponse")]
    public class MomoResponseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ResponseId { get; set; }
    
        public int? OrderID { get; set; }


        [Required, StringLength(100)]
        public string RequestId { get; set; } = string.Empty;

        [StringLength(100)]
        public string? MomoOrderId { get; set; }

        [StringLength(2000)]
        public string? PayUrl { get; set; }


        public int? ResultCode { get; set; }

        [StringLength(255)]
        public string? Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public virtual Order? Order { get; set; }
        public string ExtraData { get; internal set; }
    }
}
