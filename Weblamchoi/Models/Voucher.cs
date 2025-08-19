using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace weblamchoi.Models
{
    public class Voucher
    {
        public int VoucherID { get; set; }
        public string? Code { get; set; }
        public decimal DiscountAmount { get; set; }
        public bool IsPercentage { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; } = true;
        // Add the missing property to fix CS1061  
        [NotMapped]
        public DateTime ExpiryDate { get; set; }
    }
}
