namespace weblamchoi.Models
{
    public class Order
    {
        public int OrderID { get; set; }
        public int UserID { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public string? Status { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? VoucherCode { get; set; }
        public User? User { get; set; }
        public ICollection<OrderDetail> OrderDetails { get; set; }
        public Shipping? Shipping { get; set; }
        public Payment? Payment { get; set; }
    }

}
