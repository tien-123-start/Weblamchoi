using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace weblamchoi.Models
{
    [Table("MomoTransactions")]

    public class MomoTransaction
    {
        [Key]
        public int TransactionId { get; set; }
        public int? OrderID { get; set; }
        public string? RequestId { get; set; }
        public string? MomoOrderId { get; set; }
        public string? TransId { get; set; }
        public decimal? Amount { get; set; }
        public string? PayType { get; set; }
        public string? ResultCode { get; set; }
        public string? Message { get; set; }
        public DateTime? ResponseTime { get; set; }
        public string? Signature { get; set; }
        public string? OrderInfo { get; set; }
        public string? OrderType { get; set; }
        public DateTime ReceivedAt { get; set; } = DateTime.Now;

        public Order? Order { get; set; }
        public string? ExtraData { get; internal set; }
    }
}