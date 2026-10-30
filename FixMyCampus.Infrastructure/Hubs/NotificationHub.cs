using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace FixMyCampus.Infrastructure.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("SignalR client connected: ConnectionId {ConnectionId}, UserId {UserId}", Context.ConnectionId, userId);

        if (Context.User != null)
        {
            var roles = Context.User.FindAll(ClaimTypes.Role).Select(c => c.Value);
            foreach (var role in roles)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Role_{role}");
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (exception != null)
        {
            _logger.LogWarning(exception, "SignalR client disconnected with error: ConnectionId {ConnectionId}, UserId {UserId}", Context.ConnectionId, userId);
        }
        else
        {
            _logger.LogInformation("SignalR client disconnected cleanly: ConnectionId {ConnectionId}, UserId {UserId}", Context.ConnectionId, userId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
