using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using weblamchoi.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
namespace weblamchoi.Controllers.Admin
{

    public class UsersController : Controller
    {
        private readonly DienLanhDbContext _context;

        public UsersController(DienLanhDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.Users.ToListAsync());
        }

        public IActionResult Create()
        {
            return View();
        }
        [HttpPost]
        public IActionResult Create(User user)
        {
            if (ModelState.IsValid)
            {
                user.PasswordHash = HashPassword(user.PasswordHash); // Băm mật khẩu
                _context.Users.Add(user);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(user);
        }

        private string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        [Authorize(Roles = "User")] // Bắt buộc phải login mới vào được

        public IActionResult Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return RedirectToAction("Index", "Login");

            var user = _context.Users.Find(int.Parse(userId));
            if (user == null) return NotFound();

            var model = new UserProfileViewModel
            {
                UserID = user.UserID,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Address = user.Address
            };

            return View(model);
        }
        [HttpPost]
        [Authorize(Roles = "User")]
        public IActionResult Profile(UserProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null || model.UserID != int.Parse(userId)) return Unauthorized();

            var user = _context.Users.Find(int.Parse(userId));
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
                return View(model);

            // Nếu đổi mật khẩu
            if (!string.IsNullOrEmpty(model.CurrentPassword) && !string.IsNullOrEmpty(model.NewPassword))
            {
                var hash = HashPassword(model.CurrentPassword);
                if (user.PasswordHash != hash)
                {
                    ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không đúng.");
                    return View(model);
                }

                user.PasswordHash = HashPassword(model.NewPassword);
            }

            user.FullName = model.FullName;
            user.Phone = model.Phone;
            user.Address = model.Address;

            _context.SaveChanges();
            ViewBag.Success = "Cập nhật thành công.";
            return View(model);
        }




        public async Task<IActionResult> Edit(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, User updatedUser)
        {
            if (id != updatedUser.UserID) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.FullName = updatedUser.FullName;
            user.Email = updatedUser.Email;
            user.Phone = updatedUser.Phone;
            user.Address = updatedUser.Address;
            user.IsActive = updatedUser.IsActive;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        [Authorize(Roles = "User")]
        public async Task<IActionResult> OrderHistory(string status)
        {
            // Lấy UserID từ Claims (khi login bạn đã set ClaimTypes.NameIdentifier = UserID)
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return RedirectToAction("Index", "Login");

            var query = _context.Orders
                .Where(o => o.UserID == int.Parse(userId));

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.Status == status);
            }

            var orders = await query
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }


        [HttpPost]
        public IActionResult CancelOrder(int orderId)
        {
            var order = _context.Orders
                .Include(o => o.OrderDetails)
                .Include(o => o.Payment)
                .Include(o => o.Shipping)
                .FirstOrDefault(o => o.OrderID == orderId);

            if (order == null || order.Status != "Chờ xử lý")
            {
                return NotFound();
            }

            // Xóa các chi tiết đơn hàng
            if (order.OrderDetails != null)
                _context.OrderDetails.RemoveRange(order.OrderDetails);

            // Xóa payment nếu có
            if (order.Payment != null)
                _context.Payments.Remove(order.Payment);

            // Xóa shipping nếu có
            if (order.Shipping != null)
                _context.Shippings.Remove(order.Shipping);

            // Xóa đơn hàng
            _context.Orders.Remove(order);

            _context.SaveChanges();

            return RedirectToAction("OrderHistory");
        }

    }
}
