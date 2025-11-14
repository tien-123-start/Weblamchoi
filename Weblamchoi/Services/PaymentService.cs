using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using weblamchoi.Hubs;
using weblamchoi.Models;

namespace weblamchoi.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly DienLanhDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public PaymentService(DienLanhDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<Order?> GetOrderByMomoOrderId(string momoOrderId)
        {
            return await _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.OrderID.ToString() == momoOrderId);
        }

        public async Task CompleteMomoPayment(Order order)
        {
            order.Status = "Đã thanh toán";

            // ✅ Kiểm tra xem đã có payment chưa
            if (order.Payment == null)
            {
                order.Payment = new Payment
                {
                    OrderID = order.OrderID,
                    Amount = order.TotalAmount,   // nếu có thuộc tính TotalAmount
                    PaymentMethod = "MoMo",
                    PaymentDate = DateTime.Now,
                    Status = "Chờ xử lý"
                };
                _context.Payments.Add(order.Payment);
            }
            else
            {
                order.Payment.Status = "Completed";
                order.Payment.PaymentDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }

        public async Task SendAdminNotification(Order order, User user, string paymentMethod)
        {
            var msg = $"[ĐƠN {paymentMethod}] #{order.OrderID} - {user.FullName}";
            var noti = new Notification
            {
                Message = msg,
                Link = $"/Orders/Details/{order.OrderID}",
                IsRead = false,
                OrderID = order.OrderID,
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(noti);
            await _context.SaveChangesAsync();

            // Gửi realtime qua SignalR
            await _hubContext.Clients.Group("Admins")
                .SendAsync("ReceiveOrderNotification", msg, noti.Link, noti.NotificationId);
        }

        public async Task<User?> GetUserById(int userId)
        {
            return await _context.Users.FindAsync(userId);
        }
        public async Task<string?> CreateMomoQrPaymentAsync(Order order, string returnUrl, string notifyUrl)
        {
            if (order.TotalAmount <= 0) return null;

            string endpoint = "https://test-payment.momo.vn/v2/gateway/api/create";
            string partnerCode = "MOMOBKUN20180529";
            string accessKey = "klm05TvNBzhg7h7j";
            string secretKey = "at67qH6mk8w5Y1nAyMoYKMWACiEi2bsa";

            string orderId = order.OrderID.ToString();
            string requestId = Guid.NewGuid().ToString();
            string amount = Math.Round(order.TotalAmount ?? 0).ToString("0");
            string orderInfo = $"Thanh toán đơn hàng #{orderId}";

            string rawHash = $"accessKey={accessKey}&amount={amount}&extraData=&ipnUrl={notifyUrl}&orderId={orderId}&orderInfo={orderInfo}&partnerCode={partnerCode}&redirectUrl={returnUrl}&requestId={requestId}&requestType=captureWallet";

            using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secretKey));
            var signatureBytes = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawHash));
            string signature = BitConverter.ToString(signatureBytes).Replace("-", "").ToLower();

            var requestBody = new
            {
                partnerCode,
                partnerName = "Điện Lạnh LVM",
                storeId = "LVMStore",
                requestId,
                amount,
                orderId,
                orderInfo,
                redirectUrl = returnUrl,
                ipnUrl = notifyUrl,
                requestType = "captureWallet",
                extraData = "",
                signature,
                payType = "qr"
            };

            var response = await new HttpClient().PostAsJsonAsync(endpoint, requestBody);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            if (json != null && json.TryGetValue("payUrl", out var payUrl))
            {
                return payUrl;
            }

            return null;
        }

    }
}
