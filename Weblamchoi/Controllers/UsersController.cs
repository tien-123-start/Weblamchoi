using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using weblamchoi.Hubs;
using weblamchoi.Models;
using X.PagedList;
using X.PagedList.Extensions;

namespace weblamchoi.Controllers.Admin
{
    [Authorize]
    public class UsersController : Controller
    {
        private readonly DienLanhDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public UsersController(DienLanhDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // ========== DANH SÁCH NGƯỜI DÙNG ==========
        public IActionResult Index(int? page)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            var users = _context.Users
                .OrderBy(u => u.UserID)
                .ToPagedList(pageNumber, pageSize);

            return View(users);
        }

        // ========== TẠO TÀI KHOẢN ==========
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

        // ========== HỒ SƠ NGƯỜI DÙNG ==========
        public async Task<IActionResult> ProfileAsync(int? categoryId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Index", "Login");

            if (!int.TryParse(userId, out int parsedUserId))
                return RedirectToAction("Index", "Login");

            var user = _context.Users.Find(parsedUserId);
            if (user == null) return NotFound();

            var model = new UserProfileViewModel
            {
                UserID = user.UserID,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Address = user.Address,
                Points = user.Points
            };
            ViewBag.UserPoints = user.Points;
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.Categories = await _context.Categories.ToListAsync();

            return View(model);
        }

        [HttpPost]
        public IActionResult Profile(UserProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || model.UserID != int.Parse(userId))
                return Unauthorized();

            var user = _context.Users.Find(int.Parse(userId));
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
                return View(model);

            user.FullName = model.FullName;
            user.Phone = model.Phone;
            user.Address = model.Address;

            _context.Update(user);
            _context.SaveChanges();
            ViewBag.Success = "Cập nhật thông tin thành công.";
            ViewBag.UserPoints = user.Points;

            return View(model);
        }

        // ========== ĐỔI MẬT KHẨU ==========
        public IActionResult ChangePassword()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Index", "Login");

            var user = _context.Users.FirstOrDefault(u => u.UserID == int.Parse(userId));
            if (user == null) return NotFound();

            ViewBag.UserPoints = user.Points;

            return View(new ChangePasswordViewModel { UserID = user.UserID });
        }

        [HttpPost]
        public IActionResult ChangePassword(ChangePasswordViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || model.UserID != int.Parse(userId))
                return Unauthorized();

            var user = _context.Users.FirstOrDefault(u => u.UserID == int.Parse(userId));
            if (user == null) return NotFound();

            ViewBag.UserPoints = user.Points;

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

        // ========== SỬA NGƯỜI DÙNG ==========
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

        // ========== XÓA NGƯỜI DÙNG ==========
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ========== LỊCH SỬ ĐƠN HÀNG ==========
        public async Task<IActionResult> OrderHistory(string status, int? page)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Index", "Login");

            int uid = int.Parse(userId);
            var user = await _context.Users.FindAsync(uid);
            ViewBag.UserPoints = user?.Points ?? 0;
            var query = _context.Orders
                .Where(o => o.UserID == uid)
                .Where(o => o.Status != "Tạm giữ" && o.Status != "Chờ thanh toán");
            if (!string.IsNullOrEmpty(status))
                query = query.Where(o => o.Status == status);
            int pageSize = 5;
            int pageNumber = page ?? 1;
            var orderList = await query
         .Include(o => o.OrderDetails)
             .ThenInclude(od => od.Product)
         .OrderByDescending(o => o.OrderDate)
         .ToListAsync();
            var orders = orderList.ToPagedList(pageNumber, pageSize);
            var reviewedProductIds = await _context.Reviews
                .Where(r => r.UserID == uid)
                .Select(r => r.ProductID)
                .ToListAsync();
            ViewBag.ReviewedProductIds = reviewedProductIds;
            return View(orders);
        }

        // ========== HỦY ĐƠN HÀNG ==========
        [HttpPost]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            int userIdInt = int.Parse(userId);
            var order = await _context.Orders
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.OrderID == orderId && o.UserID == userIdInt);

            if (order == null || !(order.Status == "Chờ xử lý" || order.Status == "Chờ thanh toán"))
                return NotFound();

            order.Status = "Đã hủy";
            _context.Orders.Update(order);

            // Tạo thông báo cho admin
            var notification = new Notification
            {
                UserID = null, // ← null = thông báo chung (Admin)
                Message = $"Khách hàng {order.User?.FullName ?? "Unknown"} đã hủy đơn hàng #{order.OrderID}",
                Link = $"/Orders/Details/{order.OrderID}",               
                CreatedAt = DateTime.Now,
                IsRead = false,
                OrderID = order.OrderID
            };
            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();

            // Gửi thông báo real-time cho nhóm Admins
            await _hubContext.Clients.Group("Admins")
                .SendAsync("ReceiveOrderNotification", notification.Message, notification.Link, notification.NotificationId);

            TempData["SuccessMessage"] = "Hủy đơn hàng thành công.";
            return RedirectToAction("OrderHistory");
        }
        // ========== LẤY THÔNG BÁO ==========
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            int userIdInt = int.Parse(userId);
            var isAdmin = User.IsInRole("Admin");

            var notifications = await _context.Notifications
                .Where(n => isAdmin ? n.UserID == 0 : n.UserID == userIdInt)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .Select(n => new
                {
                    n.NotificationId,
                    n.Message,
                    n.Link,
                    n.IsRead,
                    OrderId = n.OrderID,
                    CreatedAt = n.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();

            return Json(notifications);
        }
        // Trong UsersController.cs

        // Đánh dấu một thông báo là đã đọc
        [Authorize]
        [HttpPost]
        public IActionResult MarkAsRead(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var noti = _context.Notifications.FirstOrDefault(n => n.NotificationId == id);
            if (noti == null) return NotFound();

            // Chỉ admin hoặc đúng chủ thông báo mới được đánh dấu
            if (noti.UserID != 0 && noti.UserID != int.Parse(userId))
                return Forbid();

            noti.IsRead = true;
            _context.SaveChanges();

            return Ok();
        }



        // Đánh dấu tất cả thông báo là đã đọc
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            int userIdInt = int.Parse(userId);
            var notifications = await _context.Notifications
                .Where(n => (n.UserID == userIdInt || n.UserID == 0) && !n.IsRead)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
                _context.Notifications.Update(notification);
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
