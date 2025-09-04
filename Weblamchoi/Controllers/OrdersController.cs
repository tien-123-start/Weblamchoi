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
        public async Task<IActionResult> Index(int? page)
        {
            int pageSize = 10; // số đơn hàng mỗi trang
            int pageNumber = page ?? 1;

            var orders = _context.Orders
                .Include(o => o.User)
                .Include(o => o.Shipping)
                .Include(o => o.Payment)
                .OrderByDescending(o => o.OrderDate);

            return View(orders.ToPagedList(pageNumber, pageSize));
        }

        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int id)
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

            return View(order);
        }

        [HttpPost("UpdateStatus")]
        public async Task<IActionResult> UpdateStatus(int orderId, string newStatus, int userId)
        {
            if (orderId <= 0)
            {
                TempData["ErrorMessage"] = "Mã đơn hàng không hợp lệ.";
                return RedirectToAction("Index");
            }

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.OrderID == orderId);

            if (order == null)
            {
                return NotFound();
            }

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
                            return BadRequest($"Sản phẩm {od.Product?.ProductName ?? "?"} không đủ số lượng trong kho.");
                        }
                        product.Quantity -= od.Quantity;
                    }

                    _context.RevenueReports.Add(new RevenueReport
                    {
                        Date = order.OrderDate.Date,
                        TotalRevenue = order.TotalAmount ?? 0m
                    });
                }

                _context.Orders.Update(order);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Cập nhật trạng thái đơn hàng {orderId} thành {newStatus} thành công!";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating order status for Order ID: {orderId}");
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật trạng thái đơn hàng: {ex.Message}";
            }

            return RedirectToAction("Details", new { id = orderId });
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
