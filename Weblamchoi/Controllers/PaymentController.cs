using DienLanhWeb.VNPAY;
using MailKit.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;
using weblamchoi.Hubs;
using weblamchoi.Models;
namespace DienLanhWeb.Controllers
{
    public class PaymentController : Controller
    {
        private readonly DienLanhDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        private const string VNPAY_TMNCODE = "FQT6T01R";
        private const string VNPAY_HASHKEY = "UVY2L48SC6R6QW0IEP2DM9YNU8AVL1G2";
        private const string VNPAY_URL = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

        public PaymentController(DienLanhDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public IActionResult Index()
        {
            ViewBag.TotalAmount = 100000;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> CreatePayment(int orderId, string voucherCode = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                TempData["Message"] = "Vui lòng đăng nhập để tiếp tục thanh toán.";
                return RedirectToAction("Index", "Login");
            }

            // Lấy order đã tạo từ CheckoutOnline
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.OrderID == orderId && o.UserID == userId);

            if (order == null)
            {
                TempData["Message"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction("Index", "Cart");
            }

            // Tính lại tổng tiền (đảm bảo khớp)
            decimal finalAmount = (decimal)order.TotalAmount;

            string txnRef = order.OrderID.ToString();

            var vnPay = new VnPayLibrary();
            long vnpAmount = (long)(finalAmount * 100);

            vnPay.AddRequestData("vnp_Version", "2.1.0");
            vnPay.AddRequestData("vnp_Command", "pay");
            vnPay.AddRequestData("vnp_TmnCode", VNPAY_TMNCODE);
            vnPay.AddRequestData("vnp_Amount", vnpAmount.ToString());
            vnPay.AddRequestData("vnp_CurrCode", "VND");
            vnPay.AddRequestData("vnp_TxnRef", txnRef);
            vnPay.AddRequestData("vnp_OrderInfo", $"Thanh toán đơn hàng #{txnRef}");
            vnPay.AddRequestData("vnp_OrderType", "other");
            vnPay.AddRequestData("vnp_Locale", "vn");
            vnPay.AddRequestData("vnp_ReturnUrl", Url.Action("PaymentReturn", "Payment", null, Request.Scheme));
            vnPay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnPay.AddRequestData("vnp_IpAddr", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");

            string paymentUrl = vnPay.CreateRequestUrl(VNPAY_URL, VNPAY_HASHKEY);

            return Redirect(paymentUrl); // Bây giờ sẽ chuyển đúng sang VNPAY
        }
        [AcceptVerbs("GET", "POST")]
        public async Task<IActionResult> PaymentReturn()
        {
            var vnpay = new VnPayLibrary();

            // LẤY DỮ LIỆU TỪ QUERY HOẶC FORM
            IEnumerable<KeyValuePair<string, StringValues>> vnp_Params =
                Request.Method == "POST" ? Request.Form : Request.Query;

            foreach (var item in vnp_Params)
                vnpay.AddResponseData(item.Key, item.Value.ToString());

            string vnp_SecureHash = vnpay.GetResponseData("vnp_SecureHash");
            if (!vnpay.ValidateSignature(vnp_SecureHash, VNPAY_HASHKEY))
            {
                ViewBag.Message = "Chữ ký không hợp lệ!";
                return View();
            }

            string responseCode = vnpay.GetResponseData("vnp_ResponseCode");
            string txnRef = vnpay.GetResponseData("vnp_TxnRef");
            if (!int.TryParse(txnRef, out int orderId))
            {
                ViewBag.Message = "Mã đơn hàng không hợp lệ.";
                return View();
            }

            // KIỂM TRA USER ĐĂNG NHẬP
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int currentUserId))
            {
                ViewBag.Message = "Vui lòng đăng nhập.";
                return View();
            }

            // LẤY ĐƠN HÀNG + KIỂM TRA SỞ HỮU
            var order = await _context.Orders
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.OrderID == orderId && o.UserID == currentUserId);

            if (order == null)
            {
                ViewBag.Message = "Không tìm thấy đơn hàng hoặc bạn không có quyền truy cập.";
                return View();
            }

            // CHỐNG REPLAY: ĐÃ XỬ LÝ RỒI?
            if (order.Status == "Chờ xử lý" || order.Status == "Thanh toán thất bại")
            {
                ViewBag.Message = order.Status == "Chờ xử lý"
                    ? "Đơn hàng đã được xử lý trước đó."
                    : "Đơn hàng đã thất bại trước đó.";
                ViewBag.OrderId = order.OrderID;
                return View();
            }

            // THANH TOÁN THÀNH CÔNG
            if (responseCode == "00")
            {
                order.Status = "Chờ xử lý";
                await _context.SaveChangesAsync();

                // XÓA GIỎ HÀNG
                var cartItems = await _context.Carts.Where(c => c.UserID == currentUserId).ToListAsync();
                _context.Carts.RemoveRange(cartItems);
                await _context.SaveChangesAsync();

                // GỬI THÔNG BÁO CHO ADMIN
                var notification = new Notification
                {
                    UserID = null,
                    Message = $"[THANH TOÁN THÀNH CÔNG] Đơn hàng #{order.OrderID} - {order.User?.FullName ?? "Khách"}",
                    Link = $"/Orders/Details/{order.OrderID}",
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    OrderID = order.OrderID
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                // GỬI REAL-TIME QUA SIGNALR
                await _hubContext.Clients.Group("Admins").SendAsync(
                    "ReceiveOrderNotification",
                    notification.Message,
                    notification.Link,
                    notification.NotificationId
                );

                ViewBag.Message = "Thanh toán thành công!";
                ViewBag.OrderId = order.OrderID;
            }
            else
            {
                order.Status = "Thanh toán thất bại";
                await _context.SaveChangesAsync();

                // TỰ ĐỘNG HỦY SAU 15 PHÚT
                if (order.OrderDate < DateTime.Now.AddMinutes(-15))
                {
                    order.Status = "Đã hủy (hết hạn)";
                    await _context.SaveChangesAsync();
                    ViewBag.Message = "Đơn hàng đã hết hạn và bị hủy.";
                }
                else
                {
                    ViewBag.Message = $"Thanh toán thất bại. Mã lỗi: {responseCode}";
                }
            }

            return View();
        }
    }
}
