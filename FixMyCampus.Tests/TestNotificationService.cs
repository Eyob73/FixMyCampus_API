using FixMyCampus.Application.DTOs.Notifications;
using FixMyCampus.Application.Interfaces;

namespace FixMyCampus.Tests;

public class TestNotificationService : INotificationService
{
    public List<NotificationDto> Notifications { get; } = new();

    public Task<NotificationDto> CreateAndSendNotificationAsync(Guid userId, string title, string message, string type, Guid? ticketId = null)
    {
        var dto = new NotificationDto
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TicketId = ticketId,
            Title = title,
            Message = message,
            Type = type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        Notifications.Add(dto);
        return Task.FromResult(dto);
    }

    public Task NotifyAdministratorsAsync(string title, string message, string type, Guid ticketId)
    {
        var dto = new NotificationDto
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Title = title,
            Message = message,
            Type = type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        Notifications.Add(dto);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NotificationDto>> GetUserNotificationsAsync(Guid userId, NotificationFilterRequest? filter = null)
    {
        var list = Notifications.Where(n => n.UserId == userId).ToList();
        return Task.FromResult<IReadOnlyList<NotificationDto>>(list);
    }

    public Task<int> GetUnreadCountAsync(Guid userId)
    {
        return Task.FromResult(Notifications.Count(n => n.UserId == userId && !n.IsRead));
    }

    public Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var item = Notifications.FirstOrDefault(n => n.Id == notificationId && n.UserId == userId);
        if (item != null)
        {
            item.IsRead = true;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<int> MarkAllAsReadAsync(Guid userId)
    {
        var unread = Notifications.Where(n => n.UserId == userId && !n.IsRead).ToList();
        foreach (var item in unread)
        {
            item.IsRead = true;
        }
        return Task.FromResult(unread.Count);
    }

    public Task<bool> DeleteNotificationAsync(Guid notificationId, Guid userId)
    {
        var removed = Notifications.RemoveAll(n => n.Id == notificationId && n.UserId == userId);
        return Task.FromResult(removed > 0);
    }

    public Task<int> ClearAllNotificationsAsync(Guid userId)
    {
        var count = Notifications.RemoveAll(n => n.UserId == userId);
        return Task.FromResult(count);
    }
}
