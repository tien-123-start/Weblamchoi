using System.ComponentModel.DataAnnotations.Schema;

namespace weblamchoi.Models
{
    [Table("Cart")]
    public class Cart
    {
        public int CartID { get; set; }
        public int UserID { get; set; }
        public int ProductID { get; set; }
        public int Quantity { get; set; }
        public decimal? Price { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.Now;

        public User User { get; set; }
        public Product Product { get; set; }
    }

}
