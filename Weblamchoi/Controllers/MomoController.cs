using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QRCoder;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using weblamchoi.Hubs;
using weblamchoi.Models;
using weblamchoi.Services;

namespace weblamchoi.Controllers
{
    public class MomoController : Controller
    {
        private readonly MomoSettings _momo;
        private readonly IPaymentService _paymentService;
        private readonly DienLanhDbContext _context;
        private readonly IHubContext<PaymentHub> _hubContext;
        public MomoController(
          IOptions<MomoSettings> momoSettings,
          IPaymentService paymentService,
          DienLanhDbContext context,
          IHubContext<PaymentHub> hubContext)
        {
            _momo = momoSettings.Value;
            _paymentService = paymentService;
            _context = context;
            _hubContext = hubContext;
        }
        [HttpGet]
        public IActionResult Index()
        {
            ViewBag.Message = "Chào mừng đến với thanh toán MoMo!";
            return View();
        }

        // THANH TOÁN MOMO HIỂN THỊ QR
        [HttpGet]
        public async Task<IActionResult> Create(
       int orderId,
       string orderInfo = "Thanh toán MoMo",
       long? amount = null)
        {
            // BẮT BUỘC CÓ amount
            if (!amount.HasValue || amount.Value < 1000)
            {
                ViewBag.Error = "Số tiền không hợp lệ.";
                return RedirectToAction("Index", "Cart"); // Quay lại giỏ nếu lỗi
            }

            long finalAmount = amount.Value;

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId);
            if (order == null)
            {
                ViewBag.Error = "Đơn hàng không tồn tại.";
                return RedirectToAction("Index", "Cart");
            }

            var momoOrderId = "MOMO" + DateTime.Now.Ticks;
            var requestId = momoOrderId;
            string endpoint = "https://test-payment.momo.vn/v2/gateway/api/create";

            string rawHash =
                $"accessKey={_momo.AccessKey}&amount={finalAmount}&extraData={orderId}&ipnUrl={_momo.NotifyUrl}&orderId={momoOrderId}&orderInfo={orderInfo}&partnerCode={_momo.PartnerCode}&redirectUrl={_momo.ReturnUrl}&requestId={requestId}&requestType=captureWallet";

            string signature = HmacSHA256(rawHash, _momo.SecretKey);

            var request = new
            {
                partnerCode = _momo.PartnerCode,
                accessKey = _momo.AccessKey,
                requestId,
                amount = finalAmount.ToString(),
                orderId = momoOrderId,
                orderInfo,
                redirectUrl = _momo.ReturnUrl,
                ipnUrl = _momo.NotifyUrl,
                extraData = orderId.ToString(),
                requestType = "captureWallet",
                signature,
                lang = "vi"
            };

            using var client = new HttpClient();
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(endpoint, content);
            var result = await response.Content.ReadAsStringAsync();
            var momoResult = JsonSerializer.Deserialize<MomoResponse>(result);

            if (momoResult?.resultCode == 0 && !string.IsNullOrEmpty(momoResult.payUrl))
            {
                try
                {
                    var momoResponse = new MomoResponseEntity
                    {
                        OrderID = order.OrderID,
                        RequestId = requestId,
                        MomoOrderId = momoOrderId,
                        PayUrl = momoResult.payUrl,
                        ResultCode = momoResult.resultCode,
                        Message = momoResult.message
                    };
                    _context.MomoResponses.Add(momoResponse);
                    await _context.SaveChangesAsync();
                }
                catch { }

                var qrGenerator = new QRCodeGenerator();
                var qrCodeData = qrGenerator.CreateQrCode(momoResult.payUrl, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(qrCodeData);
                var qrImage = qrCode.GetGraphic(20);
                var base64 = Convert.ToBase64String(qrImage);

                ViewBag.QRCode = $"data:image/png;base64,{base64}";
                ViewBag.PayUrl = momoResult.payUrl;
                ViewBag.OrderId = momoOrderId;
                ViewBag.Amount = finalAmount;
                ViewBag.OrderInfo = orderInfo;
                ViewBag.SystemOrderId = order.OrderID;

                return View("MomoPage"); // TRANG QR
            }

            ViewBag.Error = $"Lỗi MoMo: {momoResult?.message}";
            return RedirectToAction("Index", "Cart");
        }

        // TẠO THANH TOÁN MOMO REDIRECT (POST)
        [HttpPost]
        public IActionResult CreatePaymentMoMo(decimal amount)
        {
            if (amount < 1000) return BadRequest("Số tiền phải ≥ 1.000₫");

            string endpoint = "https://test-payment.momo.vn/v2/gateway/api/create";
            string partnerCode = _momo.PartnerCode;
            string accessKey = _momo.AccessKey;
            string secretKey = _momo.SecretKey;

            string orderId = DateTime.Now.Ticks.ToString();
            string requestId = Guid.NewGuid().ToString();
            string redirectUrl = _momo.ReturnUrl;
            string ipnUrl = _momo.NotifyUrl;
            string orderInfo = "Thanh toán đơn hàng MoMo";

            string rawHash =
                $"accessKey={accessKey}&amount={amount}&extraData=&ipnUrl={ipnUrl}&orderId={orderId}&orderInfo={orderInfo}&partnerCode={partnerCode}&redirectUrl={redirectUrl}&requestId={requestId}&requestType=captureWallet";

            string signature = HmacSHA256(rawHash, secretKey);

            var message = new
            {
                partnerCode,
                accessKey,
                requestId,
                amount = amount.ToString(),
                orderId,
                orderInfo,
                redirectUrl,
                ipnUrl,
                extraData = "",
                requestType = "captureWallet",
                signature
            };

            var json = JsonSerializer.Serialize(message);
            using var client = new HttpClient();
            var response = client.PostAsync(endpoint, new StringContent(json, Encoding.UTF8, "application/json")).Result;
            var result = response.Content.ReadAsStringAsync().Result;
            dynamic data = JsonSerializer.Deserialize<dynamic>(result);

            if (data != null && data.GetProperty("payUrl") != null)
            {
                string payUrl = data.GetProperty("payUrl").GetString();
                return Redirect(payUrl!);
            }

            return BadRequest("Không thể tạo thanh toán MoMo");
        }

        [HttpGet]
        public IActionResult Return() // ĐỔI TỪ "Result" → "Return"
        {
            var resultCode = Request.Query["resultCode"];
            var orderId = Request.Query["orderId"];
            var message = Request.Query["message"];

            ViewBag.ResultCode = resultCode;
            ViewBag.OrderId = orderId;
            ViewBag.Message = resultCode == "0" ? "Thanh toán thành công!" : message.ToString();

            return View("Return"); // Tạo file Views/Momo/Return.cshtml
        }

        [HttpPost]
        public async Task<IActionResult> Notify()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logs", "momo_notify.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            System.IO.File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {body}\n");

            try
            {
                var data = JsonSerializer.Deserialize<MomoNotify>(body);
                if (data == null) return BadRequest("Invalid JSON");

                if (VerifySignature(data) && data.resultCode == "0")
                {
                    if (!int.TryParse(data.extraData, out int orderId))
                        return BadRequest("Invalid extraData");

                    var order = await _context.Orders.FindAsync(orderId);
                    if (order == null) return BadRequest("Order not found");

                    // LƯU GIAO DỊCH
                    var transaction = new MomoTransaction
                    {
                        OrderID = order.OrderID,
                        RequestId = data.requestId,
                        MomoOrderId = data.orderId,
                        TransId = data.transId,
                        Amount = decimal.Parse(data.amount),
                        PayType = data.payType,
                        ResultCode = data.resultCode,
                        Message = data.message,
                        ResponseTime = DateTime.Parse(data.responseTime),
                        Signature = data.signature,
                        OrderInfo = data.orderInfo,
                        OrderType = data.orderType
                    };
                    _context.MomoTransactions.Add(transaction);
                    await _context.SaveChangesAsync();

                    // CẬP NHẬT ĐƠN HÀNG + GỬI THÔNG BÁO
                    if (order.Status.Contains("Chờ thanh toán"))
                    {
                        await _paymentService.CompleteMomoPayment(order);

                        var user = await _context.Users.FindAsync(order.UserID);
                        if (user != null)
                            await _paymentService.SendAdminNotification(order, user, "MoMo");

                        // GỬI SIGNALR REALTIME CHO TẤT CẢ CLIENT
                        await _hubContext.Clients.All.SendAsync(
                            "PaymentSuccess",
                            order.OrderID,
                            $"Thanh toán MoMo thành công! Đơn #{order.OrderID}"
                        );
                    }

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
            var raw =
                $"accessKey={data.accessKey}&amount={data.amount}&extraData={data.extraData}&message={data.message}&orderId={data.orderId}&orderInfo={data.orderInfo}&orderType={data.orderType}&partnerCode={data.partnerCode}&payType={data.payType}&requestId={data.requestId}&responseTime={data.responseTime}&resultCode={data.resultCode}&transId={data.transId}";
            var signature = HmacSHA256(raw, _momo.SecretKey);
            return signature.Equals(data.signature, StringComparison.OrdinalIgnoreCase);
        }

        private string HmacSHA256(string input, string key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            using var hmac = new HMACSHA256(keyBytes);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
}