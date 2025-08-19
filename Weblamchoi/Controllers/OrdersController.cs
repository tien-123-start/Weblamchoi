using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using weblamchoi.Models;
using Microsoft.AspNetCore.SignalR;
using weblamchoi.Hubs;

namespace weblamchoi.Areas.Admin.Controllers
{
    public class OrdersController : Controller
    {
        private readonly DienLanhDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public OrdersController(DienLanhDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public async Task<IActionResult> Index()
        {
            var orders = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Shipping)
                .Include(o => o.Payment)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Shipping)
                .Include(o => o.Payment)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order == null)
                return NotFound();

            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int orderId, string newStatus)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.OrderID == orderId);

            if (order == null) return NotFound();

            string previousStatus = order.Status;

            // Kiểm tra nếu trạng thái hiện tại là "Thành công" hoặc "Đã hủy"
            if (previousStatus == "Thành công" || previousStatus == "Đã hủy")
            {
                TempData["ErrorMessage"] = "Không thể cập nhật trạng thái đơn hàng đã Thành công hoặc Đã hủy.";
                return RedirectToAction("Details", new { id = orderId });
            }

            // Bắt đầu giao dịch
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Cập nhật trạng thái đơn hàng
                order.Status = newStatus;

                // Chỉ xử lý khi trạng thái đổi từ khác sang "Thành công"
                if (previousStatus != "Thành công" && newStatus == "Thành công")
                {
                    // Trừ số lượng sản phẩm trong kho
                    foreach (var orderDetail in order.OrderDetails)
                    {
                        var product = orderDetail.Product;
                        if (product != null)
                        {
                            if (product.Quantity < orderDetail.Quantity)
                            {
                                await transaction.RollbackAsync();
                                return BadRequest($"Sản phẩm {product.ProductName} không đủ số lượng trong kho.");
                            }
                            product.Quantity -= orderDetail.Quantity;
                        }
                        else
                        {
                            await transaction.RollbackAsync();
                            return BadRequest($"Sản phẩm trong chi tiết đơn hàng {orderDetail.OrderDetailID} không tồn tại.");
                        }
                    }

                    // Ghi nhận doanh thu
                    var revenueRecord = new RevenueReport
                    {
                        Date = order.OrderDate.Date,
                        TotalRevenue = order.TotalAmount ?? 0m
                    };
                    _context.RevenueReports.Add(revenueRecord);
                }

                // Lưu các thay đổi vào cơ sở dữ liệu
                await _context.SaveChangesAsync();

                // Gửi thông báo SignalR
                var notification = new Notification
                {
                    Message = $"Order ID {orderId} status updated to {newStatus}",
                    Link = $"/Admin/Orders/Details/{orderId}",
                    Type = "OrderStatus",
                    CreatedAt = DateTime.Now
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group("Admins").SendAsync("ReceiveOrderNotification", notification.Message, orderId);

                // Commit giao dịch
                await transaction.CommitAsync();

                // Ghi log
                Console.WriteLine($"Trạng thái cũ: {previousStatus}, trạng thái mới: {newStatus}");

                // Thêm thông báo thành công vào TempData
                TempData["SuccessMessage"] = $"Cập nhật trạng thái đơn hàng {orderId} thành {newStatus} thành công!";
            }
            catch (Exception ex)
            {
                // Rollback giao dịch nếu có lỗi
                await transaction.RollbackAsync();
                Console.WriteLine($"Lỗi khi cập nhật trạng thái: {ex.Message}");
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật trạng thái đơn hàng: {ex.Message}";
            }

            return RedirectToAction("Details", new { id = orderId });
        }

        [Route("api/orders/revenue")]
        [HttpGet]
        public IActionResult GetRevenueReport()
        {
            var revenueData = _context.Orders
                .Where(o => o.Status == "Thành công")
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    TotalRevenue = g.Sum(o => o.TotalAmount ?? 0m)
                })
                .ToList();

            return Ok(revenueData);
        }

        public async Task<IActionResult> RevenueReport()
        {
            var revenueDataByDay = await _context.Orders
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new RevenueReport
                {
                    Date = g.Key,
                    TotalRevenue = g.Sum(o => o.TotalAmount ?? 0m)
                })
                .ToListAsync();

            var revenueDataByMonth = await _context.Orders
                .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .Select(g => new RevenueReport
                {
                    Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                    TotalRevenue = g.Sum(o => o.TotalAmount ?? 0m)
                })
                .ToListAsync();

            ViewBag.RevenueByDay = revenueDataByDay;
            ViewBag.RevenueByMonth = revenueDataByMonth;

            return View();
        }

        private int ExtractOrderId(string link)
        {
            if (string.IsNullOrEmpty(link)) return 0;
            var parts = link.Split('/');
            return int.TryParse(parts.Last(), out int id) ? id : 0;
        }
    }
}