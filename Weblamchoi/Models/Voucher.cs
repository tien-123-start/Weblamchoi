using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace weblamchoi.Models
{
    [Table("Vouchers")]
    public class Voucher
    {
        [Key]
        public int VoucherID { get; set; }

        [Required, StringLength(50)]
        public string Code { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        public bool IsPercentage { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        // Khóa ngoại đến sản phẩm (voucher có thể áp dụng riêng cho sản phẩm)
        public int? ProductID { get; set; }
        [ForeignKey("ProductID")]
        public Product? Product { get; set; }

        public bool IsUsed { get; set; } = false;

        public DateTime? UsedAt { get; set; }

        public int? UsedByUserID { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinAmount { get; set; }

        public DateTime? ExpiryDate { get; set; }
        public int CurrentUses { get; internal set; }
        public int MaxUses { get; internal set; }
        public decimal Value { get; internal set; }
        public string Type { get; internal set; }
    }
}
