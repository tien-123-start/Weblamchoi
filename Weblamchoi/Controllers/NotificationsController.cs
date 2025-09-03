using Microsoft.AspNetCore.Mvc;
using System;
using weblamchoi.Models;

[Route("[controller]")]
public class NotificationsController : Controller
{
    private readonly DienLanhDbContext _context;

    public NotificationsController(DienLanhDbContext context)
    {
        _context = context;
    }

    // API trả JSON
    [HttpGet]
    public IActionResult GetNotifications()
    {
        var notifications = _context.Notifications
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new {
                notificationId = n.NotificationId,
                message = n.Message,
                link = n.Link,
                createdAt = n.CreatedAt,
                isRead = n.IsRead,
                orderId = n.OrderID
            }).ToList();

        return Json(notifications);
    }

    [HttpPost("MarkAsRead/{id}")]
    public IActionResult MarkAsRead(int id)
    {
        var noti = _context.Notifications.FirstOrDefault(n => n.NotificationId == id);
        if (noti == null) return NotFound();

        _context.Notifications.Remove(noti);
        _context.SaveChanges();
        return Ok();
    }

    [HttpPost("MarkAllAsRead")]
    public IActionResult MarkAllAsRead()
    {
        var notis = _context.Notifications.ToList();
        _context.Notifications.RemoveRange(notis);
        _context.SaveChanges();
        return Ok();
    }
}
