using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace weblamchoi.Models
{
    [Table("Payment")]
    public class Payment
    {
        public int PaymentID { get; set; }
        public int OrderID { get; set; }
        public string? PaymentMethod { get; set; }
        public decimal PaidAmount { get; set; }

        [Required]
        public DateTime? PaymentDate { get; set; }

        public Order Order { get; set; }
        public string? Status { get;  set; }
        public decimal? Amount { get; internal set; }
    }

}
