using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using weblamchoi.Models;

namespace weblamchoi.Controllers
{
    public class HomeController : Controller
    {
        private readonly DienLanhDbContext _context;

        public HomeController(DienLanhDbContext context)
        {
            _context = context;
        }
        [HttpGet]
        public IActionResult Chat()
        {
            return View();
        }
        // GET: /Home/Index?manufacturerId=1&categoryId=2
        public async Task<IActionResult> Index(string keyword, int? manufacturerId, int? categoryId)
        {
            // Tỉnh thành (dùng tạm ViewBag, có thể refactor thành service sau)
            ViewBag.Provinces = new List<string>
            {
                "Hà Nội", "TP. Hồ Chí Minh", "Đà Nẵng", "Hải Phòng", "Cần Thơ", "An Giang",
                "Bà Rịa - Vũng Tàu", "Bắc Giang", "Bắc Kạn", "Bạc Liêu", "Bắc Ninh", "Bến Tre",
                "Bình Định", "Bình Dương", "Bình Phước", "Bình Thuận", "Cà Mau", "Cao Bằng",
                "Đắk Lắk", "Đắk Nông", "Điện Biên", "Đồng Nai", "Đồng Tháp", "Gia Lai", "Hà Giang",
                "Hà Nam", "Hà Tĩnh", "Hải Dương", "Hậu Giang", "Hòa Bình", "Hưng Yên", "Khánh Hòa",
                "Kiên Giang", "Kon Tum", "Lai Châu", "Lâm Đồng", "Lạng Sơn", "Lào Cai", "Long An",
                "Nam Định", "Nghệ An", "Ninh Bình", "Ninh Thuận", "Phú Thọ", "Phú Yên", "Quảng Bình",
                "Quảng Nam", "Quảng Ngãi", "Quảng Ninh", "Quảng Trị", "Sóc Trăng", "Sơn La",
                "Tây Ninh", "Thái Bình", "Thái Nguyên", "Thanh Hóa", "Thừa Thiên Huế", "Tiền Giang",
                "Trà Vinh", "Tuyên Quang", "Vĩnh Long", "Vĩnh Phúc", "Yên Bái"
            };

            // Dữ liệu filter
            ViewBag.Manufacturers = await _context.Manufacturers.ToListAsync();
            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.SelectedManufacturerId = manufacturerId;
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.Keyword = keyword;

            // Query sản phẩm
            var query = _context.Products
                .Include(p => p.Manufacturer)
                .Include(p => p.Category)
                .AsQueryable();
            if (!string.IsNullOrEmpty(keyword))
            {
                keyword = keyword.Trim().ToLower();

                query = query
                    .Where(p => EF.Functions.Like(p.ProductName.ToLower(), $"%{keyword}%"))
                    .OrderByDescending(p =>
                        p.ProductName.ToLower().StartsWith(keyword) ? 2 :
                        p.ProductName.ToLower().Contains(keyword) ? 1 : 0
                    );
            }


            if (manufacturerId.HasValue)
                query = query.Where(p => p.ManufacturerID == manufacturerId.Value);

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryID == categoryId.Value);

            var products = await query
                .OrderByDescending(p => p.ProductID)
                .Take(20) // lấy 20 sản phẩm mới nhất, có thể chỉnh
                .ToListAsync();

            // Đảm bảo OriginalPrice luôn >= Price
            foreach (var p in products)
            {
                if (!p.OriginalPrice.HasValue || p.OriginalPrice <= p.Price)
                    p.OriginalPrice = p.Price;
            }

            return View(products);
        }
        [HttpGet]
        public JsonResult SearchSuggest(string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
                return Json(new List<string>());

            var suggestions = _context.Products
                .Where(p => EF.Functions.Like(p.ProductName.ToLower(), $"%{keyword.ToLower()}%"))
                .OrderByDescending(p => p.ProductName.ToLower().StartsWith(keyword.ToLower()))
                .Select(p => p.ProductName)
                .Take(5)
                .ToList();


            return Json(suggestions);
        }
        public IActionResult Tintuc()
        {
            return View();
        }

    }
}
