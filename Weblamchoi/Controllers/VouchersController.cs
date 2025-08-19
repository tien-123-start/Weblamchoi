using Microsoft.AspNetCore.Mvc;
using weblamchoi.Models;
using Microsoft.EntityFrameworkCore;

namespace weblamchoi.Controllers
{
    public class VouchersController : Controller
    {
        private readonly DienLanhDbContext _context;

        public VouchersController(DienLanhDbContext context)
        {
            _context = context;
        }

        // GET: Vouchers
        public IActionResult Index()
        {
            // Bỏ Include Product vì voucher không còn liên kết với sản phẩm
            var vouchers = _context.Vouchers.ToList();
            return View(vouchers);
        }

        // GET: Create
        public IActionResult Create()
        {
            // Bỏ ViewBag.Products vì không cần chọn sản phẩm nữa
            return View();
        }

        // POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Voucher voucher)
        {
            if (!ModelState.IsValid)
            {
                return View(voucher);
            }

            try
            {
                _context.Vouchers.Add(voucher);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi khi lưu voucher: " + ex.Message);
                return View(voucher);
            }
        }

        // GET: Edit
        public IActionResult Edit(int id)
        {
            var voucher = _context.Vouchers.Find(id);
            if (voucher == null) return NotFound();

            // Không cần ViewBag.Products nữa
            return View(voucher);
        }

        // POST: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Voucher voucher)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(voucher);
                    _context.SaveChanges();
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Lỗi khi cập nhật voucher: " + ex.Message);
                }
            }
            return View(voucher);
        }

        // Delete
        public IActionResult Delete(int id)
        {
            var voucher = _context.Vouchers.Find(id);
            if (voucher != null)
            {
                _context.Vouchers.Remove(voucher);
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }
    }
}
