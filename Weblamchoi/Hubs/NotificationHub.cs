using Microsoft.AspNetCore.SignalR;
using Polly;
using System.Text.RegularExpressions;

namespace weblamchoi.Hubs
{
    public class NotificationHub : Hub
    {
        // Gọi khi admin kết nối
        public Task JoinAdminGroup()
        {
            return Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
        }

        // Server push thông báo cho admin
        public async Task SendOrderNotification(string message, int orderId)
        {
            await Clients.Group("Admins").SendAsync("ReceiveOrderNotification", message, orderId);
        }
    }
}
