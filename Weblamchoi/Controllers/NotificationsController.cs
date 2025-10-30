using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using weblamchoi.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

[Route("api/[controller]")]
[ApiController] // THÊM: Tự động validate, trả lỗi chuẩn
[Authorize] // BẮT BUỘC ĐĂNG NHẬP
public class NotificationsController : ControllerBase
{
    private readonly DienLanhDbContext _context;

    public NotificationsController(DienLanhDbContext context)
    {
        _context = context;
    }

    // GET: api/notifications
    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var isAdmin = User.IsInRole("Admin");
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        IQueryable<Notification> query = _context.Notifications;

        if (isAdmin)
            query = query.Where(n => n.UserID == null); // Admin: thông báo chung
        else if (int.TryParse(userId, out int uid))
            query = query.Where(n => n.UserID == uid); // User: thông báo cá nhân
        else
            return Ok(new List<object>());

        var result = await query
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new
            {
                notificationId = n.NotificationId,
                message = n.Message,
                link = n.Link,
                createdAt = n.CreatedAt,
                isRead = n.IsRead
            })
            .ToListAsync();

        return Ok(result);
    }

    // POST: api/notifications/MarkAsRead/5
    // POST: api/notifications/MarkAsRead/5
    [HttpPost("MarkAsRead/{id}")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var noti = await _context.Notifications.FirstOrDefaultAsync(n => n.NotificationId == id);
        if (noti == null) return NotFound();

        var isAdmin = User.IsInRole("Admin");
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // QUYỀN HẠN:
        // - Admin: chỉ được đọc thông báo chung (UserID == null)
        // - User: chỉ được đọc thông báo của mình (UserID == userId)
        if (isAdmin)
        {
            if (noti.UserID != null) return Forbid(); // Admin không được đọc noti cá nhân
        }
        else
        {
            if (!int.TryParse(userIdClaim, out int userId) || noti.UserID != userId)
                return Forbid();
        }

        noti.IsRead = true;
        await _context.SaveChangesAsync();
        return Ok();
    }
    [HttpPost("MarkAllAsRead")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var isAdmin = User.IsInRole("Admin");
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        IQueryable<Notification> query = _context.Notifications;

        if (isAdmin)
        {
            query = query.Where(n => n.UserID == null); // Chỉ thông báo chung
        }
        else if (!int.TryParse(userIdClaim, out int userId))
        {
            return BadRequest("User ID không hợp lệ.");
        }
        else
        {
            query = query.Where(n => n.UserID == userId); // Chỉ thông báo cá nhân
        }

        var notis = await query.ToListAsync();
        notis.ForEach(n => n.IsRead = true);
        await _context.SaveChangesAsync();
        return Ok();
    }
    [HttpGet("reset-noti")]
    public async Task<IActionResult> ResetNotifications()
    {
        var adminNotis = await _context.Notifications
            .Where(n => n.UserID == null)
            .ToListAsync();

        adminNotis.ForEach(n => n.IsRead = false);
        await _context.SaveChangesAsync();

        return Ok($"Reset {adminNotis.Count} notifications");
    }

}