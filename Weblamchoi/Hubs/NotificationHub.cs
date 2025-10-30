using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace weblamchoi.Hubs
{
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (role == "Admin")
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
                // KHÔNG GỬI "joinedgroup" → TRÁNH WARNING
            }

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
                // await Clients.Caller.SendAsync("joinedgroup", $"User_{userId}"); // XÓA NẾU CÓ
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (role == "Admin")
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Admins");

            if (!string.IsNullOrEmpty(userId))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");

            await base.OnDisconnectedAsync(exception);
        }

        // (Tùy chọn) Gọi thủ công nếu cần
        public async Task JoinAdminGroup() => await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
        public async Task JoinUserGroup(string userId) => await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
    }
}