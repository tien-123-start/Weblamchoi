// Hubs/PaymentHub.cs
using Microsoft.AspNetCore.SignalR;

namespace weblamchoi.Hubs
{
    public class PaymentHub : Hub
    {
        public async Task SendPaymentSuccess(int orderId, string message)
        {
            await Clients.All.SendAsync("PaymentSuccess", orderId, message);
        }
    }
}