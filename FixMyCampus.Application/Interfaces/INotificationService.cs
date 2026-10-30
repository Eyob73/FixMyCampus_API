using FixMyCampus.Application.DTOs.Notifications;

namespace FixMyCampus.Application.Interfaces;

public interface INotificationService
{
    Task<NotificationDto> CreateAndSendNotificationAsync(Guid userId, string title, string message, string type, Guid? ticketId = null);
    Task NotifyAdministratorsAsync(string title, string message, string type, Guid ticketId);
    Task<IReadOnlyList<NotificationDto>> GetUserNotificationsAsync(Guid userId, NotificationFilterRequest? filter = null);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId);
    Task<int> MarkAllAsReadAsync(Guid userId);
    Task<bool> DeleteNotificationAsync(Guid notificationId, Guid userId);
    Task<int> ClearAllNotificationsAsync(Guid userId);
}
