using System.ComponentModel.DataAnnotations.Schema;

namespace weblamchoi.Models
{
    [Table("Shipping")]
    public class Shipping
    {
        public int ShippingID { get; set; }
        public int OrderID { get; set; }
        public string? ShippingAddress { get; set; }
        public string ShippingMethod { get; set; } = "Giao hàng tiêu chuẩn";
        public DateTime? ShippingDate { get; set; }
        public decimal ShippingFee { get; set; }
        public string? DestinationLat { get; set; }  // NEW
        public string? DestinationLng { get; set; }  // NEW
        public Order Order { get; set; }
    }
}
