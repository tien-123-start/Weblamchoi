using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using weblamchoi.Models;

public class AdminController : Controller
{
    private readonly DienLanhDbContext _context;

    public AdminController(DienLanhDbContext context)
    {
        _context = context;
    }

    // GET: Admin
    public IActionResult Index()
    {
        var admins = _context.Admins.ToList();
        return View(admins);
    }

    // GET: Admin/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Admin/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Admin admin)
    {
        if (ModelState.IsValid)
        {
            // Hash mật khẩu trước khi lưu
            admin.PasswordHash = HashPassword(admin.PasswordHash);
            _context.Admins.Add(admin);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
        return View(admin);
    }

    // GET: Admin/Edit/5
    public IActionResult Edit(int id)
    {
        var admin = _context.Admins.Find(id);
        if (admin == null) return NotFound();
        return View(admin);
    }

    // POST: Admin/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Admin admin)
    {
        if (id != admin.AdminID) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                // Nếu bạn muốn giữ mật khẩu cũ: không sửa PasswordHash
                var existing = _context.Admins.AsNoTracking().FirstOrDefault(a => a.AdminID == id);
                if (existing == null) return NotFound();

                // Nếu password được thay đổi, thì mã hóa lại
                if (existing.PasswordHash != admin.PasswordHash)
                    admin.PasswordHash = HashPassword(admin.PasswordHash);

                _context.Update(admin);
                _context.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Admins.Any(a => a.AdminID == id)) return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(admin);
    }

    // GET: Admin/Delete/5
    public IActionResult Delete(int id)
    {
        var admin = _context.Admins.Find(id);
        if (admin == null) return NotFound();

        _context.Admins.Remove(admin);
        _context.SaveChanges();
        return RedirectToAction(nameof(Index));
    }

    // ✅ Mã hóa mật khẩu SHA256
    private string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash); // dùng HEX để lưu chuỗi mã hoá
    }
    [HttpGet]
    [Route("admin/notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        var notifications = await _context.Notifications
            .Where(n => !n.IsRead && new[] { "NewOrder", "OnlineOrderPending", "OrderStatus" }.Contains(n.Type))
            .OrderByDescending(n => n.CreatedAt)
            .Take(10)
            .Select(n => new
            {
                n.NotificationId,
                n.Message,
                // Đảm bảo luôn trả về link hợp lệ, fallback nếu Link null
                Link = string.IsNullOrEmpty(n.Link) ? $"/Admin/Orders/Details/{n.NotificationId}" : n.Link,
                Time = n.CreatedAt.ToString("HH:mm dd/MM/yyyy")
            })
            .ToListAsync();

        return Json(notifications);
    }
    
    [HttpPost]
    [Route("admin/notifications/read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var notifies = await _context.Notifications.Where(n => !n.IsRead).ToListAsync();
        notifies.ForEach(n => n.IsRead = true);

        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpPost]
    [Route("admin/notifications/mark-read/{id}")]
    public async Task<IActionResult> MarkNotificationAsRead(int id)
    {
        var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.NotificationId == id);
        if (notification == null) return NotFound();

        notification.IsRead = true;
        await _context.SaveChangesAsync();

        return Ok();
    }

}
