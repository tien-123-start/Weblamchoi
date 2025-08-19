using Microsoft.AspNetCore.SignalR;

namespace weblamchoi.Hubs
{
    public class NotificationHub : Hub
    {
        public async Task SendOrderNotification(string message, int orderId)
        {
            await Clients.Group("Admins").SendAsync("ReceiveOrderNotification", message, orderId);
        }
        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }
    }
}