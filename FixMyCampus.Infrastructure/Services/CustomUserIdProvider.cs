using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace FixMyCampus.Infrastructure.Services;

public class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? connection.User?.FindFirst("sub")?.Value
            ?? connection.User?.FindFirst("id")?.Value;
    }
}
