using weblamchoi.Models;

namespace weblamchoi.Services
{
    public interface IPaymentService
    {
        Task CompleteMomoPayment(Order order);
        Task SendAdminNotification(Order order, User user, string paymentMethod);
        Task<User?> GetUserById(int userId);

        // ✅ Thêm hàm này để hỗ trợ tạo thanh toán QR MoMo
        Task<string?> CreateMomoQrPaymentAsync(Order order, string returnUrl, string notifyUrl);
    }
}
