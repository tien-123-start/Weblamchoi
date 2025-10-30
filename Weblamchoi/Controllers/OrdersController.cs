using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using weblamchoi.Models;
using weblamchoi.Hubs;
using X.PagedList.Extensions;
using X.PagedList; // Thư viện phân trang

namespace weblamchoi.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Orders")]
    public class OrdersController : Controller
    {
        private readonly DienLanhDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            DienLanhDbContext context,
            IHubContext<NotificationHub> hubContext,
            ILogger<OrdersController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? page, int? productId)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            var orders = _context.Orders
                .Include(o => o.User)
                .Include(o => o.Shipping)
                .Include(o => o.Payment)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .AsQueryable();

            if (productId.HasValue && productId > 0)
            {
                orders = orders.Where(o => o.OrderDetails.Any(od => od.ProductID == productId.Value));
                ViewBag.SearchProductId = productId.Value;
            }
            else
            {
                ViewBag.SearchProductId = null;
            }

            var pagedOrders = orders
                .OrderByDescending(o => o.OrderDate)
                .ToPagedList(pageNumber, pageSize);

            // Lưu trang hiện tại để quay lại
            ViewBag.CurrentPage = pageNumber;

            return View(pagedOrders);
        }

        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int id, int? page, int? productId)
        {
            if (id <= 0)
            {
                _logger.LogWarning($"Invalid order ID received: {id}");
                return BadRequest("Mã đơn hàng không hợp lệ.");
            }

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Shipping)
                .Include(o => o.Payment)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order == null)
            {
                _logger.LogWarning($"Order not found for ID: {id}");
                return NotFound($"Không tìm thấy đơn hàng với ID = {id}");
            }

            // Lưu lại trạng thái tìm kiếm và trang
            ViewBag.SearchProductId = productId;
            ViewBag.CurrentPage = page ?? 1;

            return View(order);
        }

        [HttpPost("UpdateStatus")]
        public async Task<IActionResult> UpdateStatus(int orderId, string newStatus)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.OrderID == orderId);

            if (order == null) return NotFound();

            string previousStatus = order.Status;

            if (previousStatus == "Thành công" || previousStatus == "Đã hủy")
            {
                TempData["ErrorMessage"] = "Không thể cập nhật trạng thái đơn hàng đã Thành công hoặc Đã hủy.";
                return RedirectToAction("Details", new { id = orderId });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                order.Status = newStatus;

                if (previousStatus != "Thành công" && newStatus == "Thành công")
                {
                    foreach (var od in order.OrderDetails)
                    {
                        var product = od.Product;
                        if (product == null || product.Quantity < od.Quantity)
                        {
                            await transaction.RollbackAsync();
                            TempData["ErrorMessage"] = $"Sản phẩm {od.Product?.ProductName ?? "?"} không đủ số lượng.";
                            return RedirectToAction("Details", new { id = orderId });
                        }
                        product.Quantity -= od.Quantity;
                    }

                    _context.RevenueReports.Add(new RevenueReport
                    {
                        Date = order.OrderDate.Date,
                        TotalRevenue = order.TotalAmount ?? 0m
                    });

                    if (order.User != null)
                    {
                        order.User.Points += 2;
                    }
                }

                _context.Orders.Update(order);

                // Kiểm tra User tồn tại trước khi thêm Notification
                if (order.User != null)
                {
                    var userExists = await _context.Users.AnyAsync(u => u.UserID == order.UserID);
                    if (userExists)
                    {
                        var notification = new Notification
                        {
                            UserID = order.UserID,
                            Message = $"Đơn hàng #{order.OrderID} đã được cập nhật sang trạng thái: {newStatus}",
                            Link = "/Users/OrderHistory",
                            Type = "OrderStatus",
                            CreatedAt = DateTime.Now,
                            IsRead = false,
                            OrderID = order.OrderID
                        };

                        _context.Notifications.Add(notification);
                        await _context.SaveChangesAsync();

                        // Gửi thông báo cho user
                        await _hubContext.Clients.Group($"User_{order.UserID}")
                            .SendAsync("ReceiveOrderNotification", notification.Message, notification.Link, notification.NotificationId);
                        _logger.LogInformation($"Sent notification to User_{order.UserID}: {notification.Message}");

                        // Gửi thông báo cho admin (nếu cần)
                        await _hubContext.Clients.Group("Admins")
                            .SendAsync("ReceiveOrderNotification", $"Đơn hàng #{order.OrderID} cập nhật trạng thái: {newStatus}", $"/Orders/Details/{order.OrderID}", notification.NotificationId);
                    }
                    else
                    {
                        _logger.LogWarning($"Không thể tạo thông báo cho đơn hàng {order.OrderID}: người dùng với UserID {order.UserID} không tồn tại.");
                        TempData["ErrorMessage"] = "Không thể tạo thông báo: người dùng không tồn tại.";
                    }
                }

                await transaction.CommitAsync();
                TempData["SuccessMessage"] = $"Cập nhật trạng thái đơn hàng {orderId} thành {newStatus} thành công!";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating order {orderId} status to {newStatus}");
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
            }

            return RedirectToAction("Details", new { id = orderId });
        }

        [HttpPost("CancelOrder")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                .Include(o => o.Payment)
                .Include(o => o.Shipping)
                .FirstOrDefaultAsync(o => o.OrderID == orderId);

            if (order == null) return NotFound();

            if (order.Status == "Thành công" || order.Status == "Đã hủy")
            {
                TempData["ErrorMessage"] = "Không thể hủy đơn hàng đã Thành công hoặc Đã hủy.";
                return RedirectToAction("Details", new { id = orderId });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Cập nhật trạng thái
                order.Status = "Đã hủy";
                _context.Orders.Update(order);

                // Xóa các liên kết nếu cần
                if (order.OrderDetails != null)
                    _context.OrderDetails.RemoveRange(order.OrderDetails);

                if (order.Payment != null)
                    _context.Payments.Remove(order.Payment);

                if (order.Shipping != null)
                    _context.Shippings.Remove(order.Shipping);

                // Tạo thông báo cho user
                if (order.User != null)
                {
                    var notification = new Notification
                    {
                        UserID = order.UserID,
                        Message = $"Đơn hàng #{order.OrderID} của bạn đã bị admin hủy.",
                        Link = "/Users/OrderHistory",
                        Type = "OrderCancelledByAdmin",
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        OrderID = order.OrderID
                    };
                    _context.Notifications.Add(notification);

                    // Gửi thông báo real-time cho user
                    await _hubContext.Clients.Group($"User_{order.UserID}")
                        .SendAsync("ReceiveOrderNotification", notification.Message, notification.Link, notification.NotificationId);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Hủy đơn hàng #{orderId} thành công.";
                return RedirectToAction("Details", new { id = orderId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error cancelling order {orderId}");
                TempData["ErrorMessage"] = $"Có lỗi xảy ra khi hủy đơn hàng: {ex.Message}";
                return RedirectToAction("Details", new { id = orderId });
            }
        }
        [HttpGet("RevenueReport")]
        public async Task<IActionResult> RevenueReport(DateTime? startDate, DateTime? endDate, string groupBy = "day")
        {
            endDate = endDate?.Date ?? DateTime.Today;
            startDate = startDate?.Date ?? endDate.Value.AddDays(-30);

            var orders = await _context.Orders
                .Where(o => o.Status == "Thành công" && o.OrderDate.Date >= startDate && o.OrderDate.Date <= endDate)
                .ToListAsync();

            // --- Dữ liệu theo groupBy người dùng chọn ---
            List<RevenueReport> revenueData = groupBy.ToLower() switch
            {
                "year" => orders
                    .GroupBy(o => o.OrderDate.Year)
                    .Select(g => new RevenueReport
                    {
                        Date = new DateTime(g.Key, 1, 1),
                        TotalRevenue = g.Sum(o => o.TotalAmount ?? 0m)
                    })
                    .OrderBy(x => x.Date).ToList(),

                "month" => orders
                    .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                    .Select(g => new RevenueReport
                    {
                        Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                        TotalRevenue = g.Sum(o => o.TotalAmount ?? 0m)
                    })
                    .OrderBy(x => x.Date).ToList(),

                _ => orders
                    .GroupBy(o => o.OrderDate.Date)
                    .Select(g => new RevenueReport
                    {
                        Date = g.Key,
                        TotalRevenue = g.Sum(o => o.TotalAmount ?? 0m)
                    })
                    .OrderBy(x => x.Date).ToList()
            };

            // --- Dữ liệu cố định theo tháng ---
            var revenueDataByMonth = orders
                .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .Select(g => new RevenueReport
                {
                    Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                    TotalRevenue = g.Sum(o => o.TotalAmount ?? 0m)
                })
                .OrderBy(x => x.Date)
                .ToList();

            // --- Dữ liệu cố định theo năm ---
            var revenueDataByYear = orders
                .GroupBy(o => o.OrderDate.Year)
                .Select(g => new RevenueReport
                {
                    Date = new DateTime(g.Key, 1, 1),
                    TotalRevenue = g.Sum(o => o.TotalAmount ?? 0m)
                })
                .OrderBy(x => x.Date)
                .ToList();

            // --- Truyền ra View ---
            ViewBag.RevenueData = revenueData;       // dữ liệu theo groupBy
            ViewBag.RevenueByMonth = revenueDataByMonth;
            ViewBag.RevenueByYear = revenueDataByYear;
            ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
            ViewBag.GroupBy = groupBy;

            return View();
        }

    }
}
