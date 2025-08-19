using Microsoft.AspNetCore.Mvc.Rendering;

namespace weblamchoi.Models
{
    public class ProductCreateViewModel
    {
        public Product Product { get; set; } = new Product(); // Khởi tạo mặc định
        public List<SelectListItem>? Categories { get; set; } // Không cần [Required]
        public List<SelectListItem>? Manufacturers { get; set; } // Không cần [Required]
        public List<Product>? BonusProducts { get; set; } // Không cần [Required]

    }
}