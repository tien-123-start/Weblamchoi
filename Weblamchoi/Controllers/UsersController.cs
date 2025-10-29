using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using weblamchoi.Models;
using X.PagedList.Extensions;
using X.PagedList; // Thư viện phân trang

namespace weblamchoi.Controllers.Admin
{
    [Authorize]
    public class UsersController : Controller
    {
        private readonly DienLanhDbContext _context;

        public UsersController(DienLanhDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(int? page)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            var users = _context.Users
                .OrderBy(u => u.UserID) // sắp xếp cho ổn định
                .ToPagedList(pageNumber, pageSize);

            return View(users);
        }

        public IActionResult Create() => View();

        [HttpPost]
        public IActionResult Create(User user)
        {
            if (ModelState.IsValid)
            {
                user.PasswordHash = HashPassword(user.PasswordHash);
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

        public IActionResult Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                Console.WriteLine("User ID is null or empty");
                return RedirectToAction("Index", "Login");
            }

            if (!int.TryParse(userId, out int parsedUserId))
            {
                Console.WriteLine("Invalid User ID format");
                return RedirectToAction("Index", "Login");
            }

            var user = _context.Users.Find(parsedUserId);
            if (user == null)
            {
                Console.WriteLine($"User not found for ID: {parsedUserId}");
                return NotFound();
            }

            var model = new UserProfileViewModel
            {
                UserID = user.UserID,
                FullName = user.FullName,
                Email = user.Email, // Lấy giá trị Email từ database
                Phone = user.Phone,
                Address = user.Address,
                Points = user.Points // ✅ thêm ở đây

            };
            ViewBag.UserPoints = user.Points; // <-- thêm dòng này

            return View(model);
        }

        [HttpPost]
        public IActionResult Profile(UserProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || model.UserID != int.Parse(userId))
            {
                Console.WriteLine("Unauthorized access or invalid UserID");
                return Unauthorized();
            }

            var user = _context.Users.Find(int.Parse(userId));
            if (user == null)
            {
                Console.WriteLine("User not found");
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                Console.WriteLine("ModelState errors: " + string.Join(", ", errors));
                ViewBag.Errors = errors;
                return View(model);
            }

            Console.WriteLine($"Current FullName: {user.FullName}, New FullName: {model.FullName}");
            user.FullName = model.FullName;
            user.Phone = model.Phone;
            user.Address = model.Address;

            try
            {
                _context.Update(user); // Sử dụng Update thay vì đặt State trực tiếp
                int changes = _context.SaveChanges();
                Console.WriteLine($"Number of changes saved: {changes}");
                if (changes > 0)
                {
                    ViewBag.Success = "Cập nhật thông tin thành công.";
                }
                else
                {
                    ViewBag.Error = "Không có thay đổi nào được lưu vào cơ sở dữ liệu.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveChanges failed: {ex.Message}");
                ViewBag.Error = $"Lỗi khi lưu dữ liệu: {ex.Message}";
            }
            ViewBag.UserPoints = user.Points; // <-- thêm dòng này

            return View(model);
        }
        public IActionResult ChangePassword()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Index", "Login");

            var user = _context.Users.FirstOrDefault(u => u.UserID == int.Parse(userId));
            if (user == null) return NotFound();

            ViewBag.UserPoints = user.Points; // gửi điểm tích lũy tới view

            var model = new ChangePasswordViewModel
            {
                UserID = int.Parse(userId)
            };
            return View(model);
        }

        [HttpPost]
        public IActionResult ChangePassword(ChangePasswordViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || model.UserID != int.Parse(userId))
                return Unauthorized();

            var user = _context.Users.FirstOrDefault(u => u.UserID == int.Parse(userId));
            if (user == null) return NotFound();

            ViewBag.UserPoints = user.Points; // gửi lại điểm sau khi submit

            if (!ModelState.IsValid)
                return View(model);

            if (HashPassword(model.CurrentPassword) != user.PasswordHash)
            {
                ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không đúng.");
                return View(model);
            }

            user.PasswordHash = HashPassword(model.NewPassword);
            _context.Update(user);
            _context.SaveChanges();

            ViewBag.Success = "Đổi mật khẩu thành công.";
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

        public async Task<IActionResult> OrderHistory(string status, int? page)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Index", "Login");

            int userIdInt = int.Parse(userId);

            // Lấy user để hiển thị Points
            var user = await _context.Users.FindAsync(userIdInt);
            ViewBag.UserPoints = user?.Points ?? 0;

            var query = _context.Orders
                .Where(o => o.UserID == userIdInt)
                .Where(o => o.Status != "Tạm giữ" && o.Status != "Chờ thanh toán");

            if (!string.IsNullOrEmpty(status))
                query = query.Where(o => o.Status == status);

            int pageSize = 5;
            int pageNumber = page ?? 1;

            // Lấy data bằng EF Core async trước
            var orderList = await query
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            // Rồi phân trang
            var orders = orderList.ToPagedList(pageNumber, pageSize);

            var reviewedProductIds = await _context.Reviews
               .Where(r => r.UserID == userIdInt)
               .Select(r => r.ProductID)
               .ToListAsync();
            ViewBag.ReviewedProductIds = reviewedProductIds;

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

            if (order == null || !(order.Status == "Chờ xử lý" || order.Status == "Chờ thanh toán"))
                return NotFound();

            if (order.OrderDetails != null)
                _context.OrderDetails.RemoveRange(order.OrderDetails);

            if (order.Payment != null)
                _context.Payments.Remove(order.Payment);

            if (order.Shipping != null)
                _context.Shippings.Remove(order.Shipping);

            _context.Orders.Remove(order);
            _context.SaveChanges();

            return RedirectToAction("OrderHistory");
        }
    }
}