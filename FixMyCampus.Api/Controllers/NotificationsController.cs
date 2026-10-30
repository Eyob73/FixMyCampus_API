using System.Security.Claims;
using FixMyCampus.Application.DTOs.Notifications;
using FixMyCampus.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixMyCampus.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private Guid CurrentUserId
    {
        get
        {
            var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("sub")
                          ?? User.FindFirstValue("id");

            if (string.IsNullOrEmpty(claimValue) || !Guid.TryParse(claimValue, out var userId))
            {
                throw new UnauthorizedAccessException("User identification claim is missing or invalid.");
            }

            return userId;
        }
    }

    /// <summary>
    /// Get the authenticated user's notifications (newest first, with optional unread filter and pagination)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] NotificationFilterRequest? filter)
    {
        var notifications = await _notificationService.GetUserNotificationsAsync(CurrentUserId, filter);
        return Ok(notifications);
    }

    /// <summary>
    /// Get unread notification count for the authenticated user
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await _notificationService.GetUnreadCountAsync(CurrentUserId);
        return Ok(new { count, unreadCount = count });
    }

    /// <summary>
    /// Mark a notification belonging to the authenticated user as read
    /// </summary>
    [HttpPatch("{id}/read")]
    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var success = await _notificationService.MarkAsReadAsync(id, CurrentUserId);
        if (!success)
        {
            return NotFound(new { message = "Notification not found or access denied." });
        }

        return Ok(new { message = "Notification marked as read." });
    }

    /// <summary>
    /// Mark all notifications belonging to the authenticated user as read
    /// </summary>
    [HttpPatch("mark-all-read")]
    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var count = await _notificationService.MarkAllAsReadAsync(CurrentUserId);
        return Ok(new { message = "All notifications marked as read.", count });
    }

    /// <summary>
    /// Delete a single notification belonging to the authenticated user
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        var success = await _notificationService.DeleteNotificationAsync(id, CurrentUserId);
        if (!success)
        {
            return NotFound(new { message = "Notification not found or access denied." });
        }

        return Ok(new { message = "Notification deleted successfully." });
    }

    /// <summary>
    /// Clear all notifications belonging to the authenticated user
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> ClearNotifications()
    {
        var count = await _notificationService.ClearAllNotificationsAsync(CurrentUserId);
        return Ok(new { message = "All notifications cleared.", count });
    }
}
