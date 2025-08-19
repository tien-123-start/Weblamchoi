using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using weblamchoi.Models;
using System.Threading.Tasks;
using System.Linq;

namespace weblamchoi.Controllers.Admin
{
    public class ReviewsController : Controller
    {
        private readonly DienLanhDbContext _context;

        public ReviewsController(DienLanhDbContext context)
        {
            _context = context;
        }

        // Danh sách sản phẩm để chọn xem bình luận
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.Reviews) // Thêm Include để lấy số lượng bình luận
                .OrderBy(p => p.ProductName)
                .ToListAsync();

            return View(products);
        }

        // Xem bình luận của sản phẩm theo id sản phẩm
        public async Task<IActionResult> ByProduct(int productId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductID == productId);

            if (product == null)
                return NotFound();

            var reviews = await _context.Reviews
                .Where(r => r.ProductID == productId)
                .Include(r => r.User)
                .OrderByDescending(r => r.ReviewDate)
                .ToListAsync();

            ViewBag.Product = product;
            return View(reviews);
        }

        // Xóa bình luận
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null)
                return NotFound();

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ByProduct), new { productId = review.ProductID });
        }
    }
}
