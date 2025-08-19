using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace weblamchoi.Models
{
    public class Product
    {
        public int ProductID { get; set; }
        public string? ProductName { get; set; }
        public int CategoryID { get; set; }
        public int? ManufacturerID { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string? Description { get; set; }
        public string? ImageURL { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsAvailable { get; set; } = true;
        public decimal? OriginalPrice { get; set; }
        public int? BonusProductID { get; set; }  // khóa ngoại
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? DiscountPercentage { get; set; } // Phần trăm giảm giá
        // Khuyến mãi (quà tặng đi kèm)

        [ForeignKey("BonusProductID")]
        public Product? BonusProduct { get; set; }
        public ICollection<Product>? UsedAsBonusBy { get; set; }

        public Category? Category { get; set; }
        public Manufacturer? Manufacturer { get; set; }

        public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        public ICollection<Cart> Carts { get; set; } = new List<Cart>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<ProductThumbnail> Thumbnails { get; set; } = new List<ProductThumbnail>();
        public ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();
    }
}
