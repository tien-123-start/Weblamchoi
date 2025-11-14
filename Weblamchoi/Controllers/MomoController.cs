using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QRCoder;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using weblamchoi.Hubs;
using weblamchoi.Models;
using weblamchoi.Services;
using System.Globalization; // cần để format số khi tạo raw signature


namespace weblamchoi.Controllers
{
    public class MomoController : Controller
    {
        private readonly MomoSettings _momo;
        private readonly IPaymentService _paymentService;
        private readonly DienLanhDbContext _context;
        private readonly IHubContext<PaymentHub> _hubContext;
        private readonly HttpClient _httpClient;

        public MomoController(
            IOptions<MomoSettings> momoSettings,
            IPaymentService paymentService,
            DienLanhDbContext context,
            IHubContext<PaymentHub> hubContext,
            IHttpClientFactory httpClientFactory) // Add IHttpClientFactory parameter
        {
            _momo = momoSettings.Value;
            _paymentService = paymentService;
            _context = context;
            _hubContext = hubContext;
            _httpClient = httpClientFactory.CreateClient(); // Properly initialize HttpClient
        }

        [HttpGet]
        public IActionResult Index()
        {
            ViewBag.Message = "Chào mừng đến với thanh toán MoMo!";
            return View();
        }

        // HIỂN THỊ QR MOMO
        [HttpGet]
        public async Task<IActionResult> Create(int orderId, string orderInfo = "Thanh toán MoMo", long? amount = null)
        {
            // validate và tìm order giống như bạn đã làm
            if (!amount.HasValue || amount < 1000 || amount > 100_000_000)
                return BadRequest(new { error = "Số tiền không hợp lệ (tối thiểu 1.000đ, tối đa 100 triệu)." });

            var order = await _context.Orders
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.OrderID == orderId);

            if (order == null)
                return NotFound(new { error = $"Không tìm thấy đơn hàng #{orderId}." });

            if (order.Status.Contains("Đã thanh toán") || order.Status.Contains("Hủy"))
                return BadRequest(new { error = $"Đơn hàng #{orderId} đã được xử lý hoặc hủy." });

            if (amount.Value != (long)Math.Round((decimal)order.TotalAmount))
                return BadRequest(new { error = "Số tiền thanh toán không khớp với đơn hàng." });

            var momoOrderId = "MOMO" + DateTime.Now.Ticks;
            var requestId = momoOrderId;
            var extraData = Convert.ToBase64String(Encoding.UTF8.GetBytes(orderId.ToString()));

            var rawHash = $"accessKey={_momo.AccessKey}&amount={amount}&extraData={extraData}&ipnUrl={_momo.NotifyUrl}&orderId={momoOrderId}&orderInfo={orderInfo}&partnerCode={_momo.PartnerCode}&redirectUrl={_momo.ReturnUrl}&requestId={requestId}&requestType=captureWallet";
            var signature = HmacSHA256(rawHash, _momo.SecretKey);

            var request = new
            {
                partnerCode = _momo.PartnerCode,
                accessKey = _momo.AccessKey,
                requestId,
                amount = amount.Value,
                orderId = momoOrderId,
                orderInfo,
                redirectUrl = _momo.ReturnUrl,
                ipnUrl = _momo.NotifyUrl,
                extraData,
                requestType = "captureWallet",
                signature,
                lang = "vi"
            };

            HttpResponseMessage response;
            try
            {
                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = null });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                response = await _httpClient.PostAsync("https://test-payment.momo.vn/v2/gateway/api/create", content);
            }
            catch (Exception ex)
            {
                await LogErrorAsync("momo_error.log", $"API CALL FAILED: {ex.Message}");
                return StatusCode(500, new { error = "Không thể kết nối đến MoMo. Vui lòng thử lại sau." });
            }

            var result = await response.Content.ReadAsStringAsync();
            await LogErrorAsync("momo_response.log", result);

            if (!response.IsSuccessStatusCode)
                return StatusCode(500, new { error = $"MoMo API lỗi: {response.StatusCode}" });

            var momoResult = JsonSerializer.Deserialize<MomoResponse>(result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (momoResult?.resultCode != 0 || string.IsNullOrEmpty(momoResult.payUrl))
            {
                return BadRequest(new { error = $"MoMo lỗi: {momoResult?.message ?? "Không rõ"}", code = momoResult?.resultCode });
            }

            var qr = new QRCodeGenerator().CreateQrCode(momoResult.payUrl, QRCodeGenerator.ECCLevel.Q);
            var qrBytes = new PngByteQRCode(qr).GetGraphic(20);
            ViewBag.QRCode = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}";

            ViewBag.PayUrl = momoResult.payUrl;
            ViewBag.OrderId = momoOrderId;
            ViewBag.Amount = amount.Value;
            ViewBag.OrderInfo = orderInfo;
            ViewBag.SystemOrderId = order.OrderID;
            ViewBag.UserName = order.User?.FullName ?? order.User?.Email?.Split('@')[0] ?? "Khách";

            // LƯU MOMO RESPONSE (bản log) + TẠO MomoTransaction ban đầu để Return có thể tìm
            try
            {
                _context.MomoResponses.Add(new MomoResponseEntity
                {
                    OrderID = order.OrderID,
                    RequestId = requestId,
                    MomoOrderId = momoOrderId,
                    PayUrl = momoResult.payUrl,
                    ResultCode = momoResult.resultCode,
                    Message = momoResult.message,
                    CreatedAt = DateTime.Now,
                    ExtraData = extraData
                });

                // Tạo transaction sơ khai (đảm bảo MomoOrderId luôn có giá trị)
                var initialTran = new MomoTransaction
                {
                    OrderID = order.OrderID,
                    RequestId = requestId,
                    MomoOrderId = momoOrderId, // <-- always set
                    Amount = amount.Value,
                    OrderInfo = orderInfo,
                    OrderType = "",
                    PayType = "",
                    ResultCode = momoResult.resultCode.ToString(),
                    Message = momoResult.message,
                    ExtraData = extraData,
                    ReceivedAt = DateTime.Now
                };

                _context.MomoTransactions.Add(initialTran);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await LogErrorAsync("momo_response.log", $"DB SAVE ERROR: {ex.Message}\n{ex.StackTrace}");
                // Không return lỗi → vẫn cho thanh toán
            }

            return View("MomoPage");
        }

        // === HÀM LOG RIÊNG (DỄ DÙNG) ===
        private async Task LogErrorAsync(string fileName, string message)
        {
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logs", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await System.IO.File.AppendAllTextAsync(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}\n");
        }

            [HttpGet]
            [Route("Momo/Return")]
            public async Task<IActionResult> Return(
         string orderId,      // MoMo orderId (MOMO...)
         string requestId,
         string extraData,    // Base64
         string resultCode,
         string message,      // MoMo gửi ở Return
         string transId,      // BẮT BUỘC
         string signature)
              {
            // decode extraData
            int orderIdInt = 0;
            try
            {
                if (!string.IsNullOrEmpty(extraData))
                {
                    var bytes = Convert.FromBase64String(extraData);
                    var decoded = Encoding.UTF8.GetString(bytes);
                    int.TryParse(decoded, out orderIdInt);
                }
            }
            catch
            {
                // ignore -> orderIdInt = 0
            }

            // TÌM TRANSACTION: ưu tiên tìm bằng MomoOrderId, fallback theo OrderID
            MomoTransaction? transaction = null;
            if (!string.IsNullOrEmpty(orderId))
            {
                transaction = await _context.MomoTransactions
                    .Include(t => t.Order)
                        .ThenInclude(o => o.User)
                    .Include(t => t.Order)
                        .ThenInclude(o => o.Payment)
                    .Include(t => t.Order)                 // include OrderDetails và Product
                        .ThenInclude(o => o.OrderDetails)
                            .ThenInclude(od => od.Product)
                    .FirstOrDefaultAsync(t => t.MomoOrderId == orderId);
            }

            if (transaction == null && orderIdInt > 0)
            {
                transaction = await _context.MomoTransactions
                    .Include(t => t.Order)
                        .ThenInclude(o => o.User)
                    .Include(t => t.Order)
                        .ThenInclude(o => o.Payment)
                    .Include(t => t.Order)
                        .ThenInclude(o => o.OrderDetails)
                            .ThenInclude(od => od.Product)
                    .FirstOrDefaultAsync(t => t.OrderID == orderIdInt);
            }

            if (transaction == null)
            {
                TempData["Message"] = "Không tìm thấy giao dịch!";
                return RedirectToAction("Index", "Cart");
            }


            // Update transaction with returned info (đảm bảo không lưu NULL vào cột NOT NULL)
            transaction.MomoOrderId = transaction.MomoOrderId ?? orderId ?? "UNKNOWN";
            transaction.TransId = transId ?? transaction.TransId;
            transaction.Signature = signature ?? transaction.Signature;
            transaction.ResponseTime = DateTime.Now;
            transaction.ResultCode = resultCode ?? transaction.ResultCode;
            transaction.Message = message ?? transaction.Message;

            // XÁC THỰC CHỮ KÝ: xây raw dùng các trường thực tế (lưu ý: cấu trúc phải theo spec MoMo nếu bạn muốn strict)
            // Ở đây ta build raw bằng các trường có sẵn để tăng khả năng khớp
            var amountStr = transaction.Amount.HasValue ? transaction.Amount.Value.ToString("0", CultureInfo.InvariantCulture) : "0";
            var rawSignature = $"accessKey={_momo.AccessKey}&amount={amountStr}&extraData={extraData}&message={message}&orderId={orderId}&orderInfo={transaction.OrderInfo}&orderType={transaction.OrderType}&partnerCode={_momo.PartnerCode}&payType={transaction.PayType}&requestId={requestId}&resultCode={resultCode}&transId={transId}";

            var expectedSignature = HmacSHA256(rawSignature, _momo.SecretKey);

            if (!string.Equals(expectedSignature, signature, StringComparison.OrdinalIgnoreCase))
            {
                // Log chi tiết để debug (signature mismatch)
                await LogErrorAsync("momo_response.log", $"Signature mismatch on Return. expected={expectedSignature} actual={signature} raw={rawSignature}");
                TempData["Message"] = "Chữ ký không hợp lệ!";
                return RedirectToAction("Index", "Cart");
            }

            if (resultCode == "0")
            {
                // Xóa giỏ hàng
                var cartItems = _context.Carts.Where(c => c.UserID == transaction.Order.UserID);
                _context.Carts.RemoveRange(cartItems);

                // Cập nhật trạng thái order và payment
                transaction.Order.Status = "Đã thanh toán";
                if (transaction.Order.Payment != null)
                    transaction.Order.Payment.Status = "Completed";

                // Lưu thay đổi ngay để OrderHistory kịp nhận
                await _context.SaveChangesAsync();
            }


            // Xử lý thành công
            if (transaction.Order != null)
            {
                var userId = transaction.Order.UserID;
                var userCartItems = _context.Carts.Where(c => c.UserID == userId);
                _context.Carts.RemoveRange(userCartItems);

                transaction.Order.Status = "Đã thanh toán";
                if (transaction.Order.Payment != null)
                    transaction.Order.Payment.Status = "Completed";

                await _context.SaveChangesAsync();
            }

            // đăng nhập lại user (giữ nguyên code của bạn)
            var user = transaction.Order.User;
            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
            new Claim(ClaimTypes.Name, user.FullName ?? user.Email.Split('@')[0]),
            new Claim(ClaimTypes.Email, user.Email)
        };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true }
            );

            await _hubContext.Clients.All.SendAsync(
                "PaymentSuccess",
                transaction.Order.OrderID,
                $"Đơn hàng #{transaction.Order.OrderID} đã thanh toán thành công qua MoMo!"
            );

            TempData["Message"] = "Thanh toán MoMo thành công!";
            return RedirectToAction("Index", "Cart");
        }

        [HttpPost]
        public async Task<IActionResult> Notify()
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logs", "momo_notify.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            System.IO.File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {body}\n");

            try
            {
                var notifyData = JsonSerializer.Deserialize<MomoNotify>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (notifyData == null) return BadRequest("Invalid JSON");

                if (VerifySignature(notifyData) && notifyData.resultCode == 0)
                {
                    // Decode extraData
                    int orderId = 0;
                    if (!string.IsNullOrEmpty(notifyData.extraData))
                    {
                        try
                        {
                            var bytes = Convert.FromBase64String(notifyData.extraData);
                            var decoded = Encoding.UTF8.GetString(bytes);
                            int.TryParse(decoded, out orderId);
                        }
                        catch
                        {
                            await LogErrorAsync("momo_notify.log", $"Cannot decode extraData: {notifyData.extraData}");
                            return BadRequest("Invalid extraData");
                        }
                    }

                    var order = await _context.Orders
                       .Include(o => o.OrderDetails)
                           .ThenInclude(od => od.Product)
                       .Include(o => o.Payment)
                       .Include(o => o.User)
                       .FirstOrDefaultAsync(o => o.OrderID == orderId);


                    if (order == null) return BadRequest("Order not found");

                    // Lưu MomoTransaction
                    var transaction = new MomoTransaction
                    {
                        OrderID = order.OrderID,
                        RequestId = notifyData.requestId ?? "",
                        MomoOrderId = notifyData.orderId ?? "UNKNOWN",
                        TransId = notifyData.transId.ToString(),
                        Amount = notifyData.amount,
                        PayType = notifyData.payType ?? "",
                        ResultCode = notifyData.resultCode.ToString(),
                        Message = notifyData.message ?? "",
                        ResponseTime = DateTimeOffset.FromUnixTimeMilliseconds(notifyData.responseTime).UtcDateTime,
                        Signature = notifyData.signature ?? "",
                        OrderInfo = notifyData.orderInfo ?? "",
                        OrderType = notifyData.orderType ?? "",
                        ExtraData = notifyData.extraData ?? ""
                    };
                    _context.MomoTransactions.Add(transaction);

                    // Cập nhật trạng thái Order và Payment ngay
                    if (order.Status.Contains("Chờ thanh toán") || order.Status.Contains("Chờ xử lý"))
                    {
                        order.Status = "Đã thanh toán";
                        if (order.Payment != null)
                            order.Payment.Status = "Completed";
                    }

                    await _context.SaveChangesAsync();

                    // Thông báo admin/user
                    if (order.User != null)
                        await _paymentService.SendAdminNotification(order, order.User, "MoMo");

                    await _hubContext.Clients.All.SendAsync(
                        "PaymentSuccess",
                        order.OrderID,
                        $"Thanh toán MoMo thành công! Đơn #{order.OrderID}"
                    );

                    return Ok("OK");
                }
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logPath, $"EXCEPTION: {ex.Message}\n{ex.StackTrace}\n");
            }

            return BadRequest("Invalid");
        }


        private bool VerifySignature(MomoNotify data)
        {
            var raw = $"accessKey={data.accessKey}&amount={data.amount}&extraData={data.extraData}&message={data.message}&orderId={data.orderId}&orderInfo={data.orderInfo}&orderType={data.orderType}&partnerCode={data.partnerCode}&payType={data.payType}&requestId={data.requestId}&responseTime={data.responseTime}&resultCode={data.resultCode}&transId={data.transId}";
            var signature = HmacSHA256(raw, _momo.SecretKey);
            return signature.Equals(data.signature, StringComparison.OrdinalIgnoreCase);
        }

        private string HmacSHA256(string input, string key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            using var hmac = new HMACSHA256(keyBytes);
            return BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(input))).Replace("-", "").ToLower();
        }
    }
}
