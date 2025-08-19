using DienLanhWeb.VNPAY;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public async Task<IActionResult> CreatePayment(string voucherCode = null)
        {
            // ✅ Lấy userId từ JWT token claim
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                TempData["Message"] = "Vui lòng đăng nhập để tiếp tục thanh toán.";
                return RedirectToAction("Index", "Login");
            }

            // ✅ Lấy tổng tiền từ DB (hoặc giỏ hàng của user)
            var cartItems = await _context.Carts
                .Include(c => c.Product)
                .Where(c => c.UserID == userId)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["Message"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction("Index", "Cart");
            }

            decimal finalAmount = cartItems.Sum(c => (c.Price ?? c.Product.Price) * c.Quantity);

            // ✅ Tạo mã giao dịch txnRef chính là OrderID (sau khi lưu Order)
            var order = new Order
            {
                UserID = userId,
                TotalAmount = finalAmount,
                Status = "Chờ thanh toán",
                OrderDate = DateTime.Now,
                VoucherCode = voucherCode,
                OrderDetails = cartItems.Select(c => new OrderDetail
                {
                    ProductID = c.ProductID,
                    Quantity = c.Quantity,
                    UnitPrice = c.Price ?? c.Product?.Price ?? 0m
                }).ToList()
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // ✅ Sau khi SaveChanges, OrderID mới được sinh ra
            string txnRef = order.OrderID.ToString();

            // ✅ Tạo request VNPay
            var vnPay = new VnPayLibrary();
            long vnpAmount = (long)(finalAmount * 100);
            vnPay.AddRequestData("vnp_Version", "2.1.0");
            vnPay.AddRequestData("vnp_Command", "pay");
            vnPay.AddRequestData("vnp_TmnCode", VNPAY_TMNCODE);
            vnPay.AddRequestData("vnp_Amount", vnpAmount.ToString());
            vnPay.AddRequestData("vnp_CurrCode", "VND");
            vnPay.AddRequestData("vnp_TxnRef", txnRef); // dùng OrderID làm TxnRef
            vnPay.AddRequestData("vnp_OrderInfo", $"Thanh toán đơn hàng #{txnRef}");
            vnPay.AddRequestData("vnp_OrderType", "other");
            vnPay.AddRequestData("vnp_Locale", "vn");
            vnPay.AddRequestData("vnp_ReturnUrl", Url.Action("PaymentReturn", "Payment", null, Request.Scheme));
            vnPay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnPay.AddRequestData("vnp_IpAddr", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");

            string paymentUrl = vnPay.CreateRequestUrl(VNPAY_URL, VNPAY_HASHKEY);
            return Redirect(paymentUrl);
        }


        [AcceptVerbs("GET", "POST")]
        public async Task<IActionResult> PaymentReturn()
        {
            var vnpay = new VnPayLibrary();
            Dictionary<string, string> vnp_Params = new Dictionary<string, string>();

            if (Request.Method == "POST")
            {
                foreach (var key in Request.Form.Keys)
                    vnp_Params[key] = Request.Form[key];
            }
            else
            {
                foreach (var key in Request.Query.Keys)
                    vnp_Params[key] = Request.Query[key];
            }

            foreach (var param in vnp_Params)
            {
                vnpay.AddResponseData(param.Key, param.Value);
            }

            string vnp_SecureHash = vnpay.GetResponseData("vnp_SecureHash");
            bool isValid = vnpay.ValidateSignature(vnp_SecureHash, VNPAY_HASHKEY);

            if (!isValid)
            {
                ViewBag.Message = "❌ Chữ ký không hợp lệ!";
                return View();
            }

            string responseCode = vnpay.GetResponseData("vnp_ResponseCode");
            string txnRef = vnpay.GetResponseData("vnp_TxnRef");

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderID.ToString() == txnRef); // đổi sang Id (theo model của bạn)

            if (order == null)
            {
                ViewBag.Message = "❌ Không tìm thấy đơn hàng.";
                return View();
            }

            if (responseCode == "00")
            {
                order.Status = "Đã thanh toán";
                await _context.SaveChangesAsync();

                // ✅ Lấy userId từ JWT Token claims
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null)
                {
                    return RedirectToAction("Index", "Login "); // Nếu chưa đăng nhập
                }

                if (int.TryParse(userId, out int uid))
                {
                    var cartItems = _context.Carts.Where(c => c.UserID == uid);
                    _context.Carts.RemoveRange(cartItems);
                    await _context.SaveChangesAsync();
                }

                ViewBag.Message = "✅ Thanh toán và cập nhật đơn hàng thành công!";
            }
            else
            {
                order.Status = "Thanh toán thất bại";
                await _context.SaveChangesAsync();
                ViewBag.Message = $"❌ Thanh toán thất bại. Mã lỗi: {responseCode}";
            }

            return View();
        }

    }
}
