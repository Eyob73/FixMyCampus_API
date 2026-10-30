using FixMyCampus.Application.DTOs.Notifications;
using FixMyCampus.Application.Interfaces;
using FixMyCampus.Domain.Entities;
using FixMyCampus.Infrastructure.Hubs;
using FixMyCampus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FixMyCampus.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext context,
        IHubContext<NotificationHub> hubContext,
        UserManager<ApplicationUser> userManager,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<NotificationDto> CreateAndSendNotificationAsync(
        Guid userId,
        string title,
        string message,
        string type,
        Guid? ticketId = null)
    {
        // Duplicate prevention: avoid inserting duplicate notifications for the same user, ticket, and event within 5 minutes
        if (ticketId.HasValue)
        {
            var recentDuplicate = await _context.Notifications
                .Where(n => n.UserId == userId && n.TicketId == ticketId.Value && n.Type == type)
                .OrderByDescending(n => n.CreatedAt)
                .FirstOrDefaultAsync();

            if (recentDuplicate != null && recentDuplicate.CreatedAt > DateTime.UtcNow.AddMinutes(-5))
            {
                _logger.LogInformation("Skipping duplicate notification for User {UserId}, Ticket {TicketId}, Type {Type}", userId, ticketId, type);
                return MapToDto(recentDuplicate);
            }
        }

        var notification = new Notification
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

        // 1. Database first: Persist to PostgreSQL
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        var dto = MapToDto(notification);

        // Populate ticket details if ticketId exists
        if (ticketId.HasValue)
        {
            var ticket = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId.Value);
            if (ticket != null)
            {
                dto.TicketTitle = ticket.Title;
                dto.TicketCategory = ticket.Category;
                dto.TicketBuilding = ticket.Building;
                dto.TicketStatus = ticket.Status.ToString();
            }
        }

        // 2. Real-time delivery via SignalR (fire-and-forget safely; SignalR error must not fail business flow)
        try
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", dto);
            var unreadCount = await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            await _hubContext.Clients.User(userId.ToString()).SendAsync("UpdateUnreadCount", unreadCount);
            _logger.LogInformation("Delivered real-time notification {NotificationId} to user {UserId}", notification.Id, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deliver SignalR notification to user {UserId}. Notification is safely stored in database.", userId);
        }

        return dto;
    }

    public async Task NotifyAdministratorsAsync(string title, string message, string type, Guid ticketId)
    {
        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        if (admins == null || admins.Count == 0)
        {
            _logger.LogWarning("No administrators found in role Admin to receive notification for ticket {TicketId}", ticketId);
            return;
        }

        var ticket = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId);
        var createdNotifications = new List<(Notification entity, Guid adminId)>();

        foreach (var admin in admins)
        {
            // Duplicate prevention for each admin
            var recentDuplicate = await _context.Notifications
                .Where(n => n.UserId == admin.Id && n.TicketId == ticketId && n.Type == type)
                .OrderByDescending(n => n.CreatedAt)
                .FirstOrDefaultAsync();

            if (recentDuplicate != null && recentDuplicate.CreatedAt > DateTime.UtcNow.AddMinutes(-5))
            {
                _logger.LogInformation("Skipping duplicate admin notification for Admin {AdminId}, Ticket {TicketId}, Type {Type}", admin.Id, ticketId, type);
                continue;
            }

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = admin.Id,
                TicketId = ticketId,
                Title = title,
                Message = message,
                Type = type,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            createdNotifications.Add((notification, admin.Id));
        }

        if (createdNotifications.Count > 0)
        {
            await _context.SaveChangesAsync();
        }

        // Deliver to each administrator in real-time
        foreach (var (notification, adminId) in createdNotifications)
        {
            var dto = MapToDto(notification);
            if (ticket != null)
            {
                dto.TicketTitle = ticket.Title;
                dto.TicketCategory = ticket.Category;
                dto.TicketBuilding = ticket.Building;
                dto.TicketStatus = ticket.Status.ToString();
            }

            try
            {
                await _hubContext.Clients.User(adminId.ToString()).SendAsync("ReceiveNotification", dto);
                var unreadCount = await _context.Notifications.CountAsync(n => n.UserId == adminId && !n.IsRead);
                await _hubContext.Clients.User(adminId.ToString()).SendAsync("UpdateUnreadCount", unreadCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deliver SignalR notification to admin {AdminId}. Notification is stored in database.", adminId);
            }
        }
    }

    public async Task<IReadOnlyList<NotificationDto>> GetUserNotificationsAsync(Guid userId, NotificationFilterRequest? filter = null)
    {
        var query = _context.Notifications
            .Include(n => n.Ticket)
            .Where(n => n.UserId == userId)
            .AsNoTracking()
            .AsQueryable();

        if (filter != null)
        {
            if (filter.UnreadOnly.HasValue && filter.UnreadOnly.Value)
            {
                query = query.Where(n => !n.IsRead);
            }

            if (!string.IsNullOrWhiteSpace(filter.Type))
            {
                query = query.Where(n => n.Type == filter.Type);
            }
        }

        query = query.OrderByDescending(n => n.CreatedAt);

        if (filter != null && filter.PageSize > 0)
        {
            var skip = Math.Max(0, (filter.PageNumber - 1) * filter.PageSize);
            query = query.Skip(skip).Take(filter.PageSize);
        }

        var notifications = await query.ToListAsync();

        return notifications.Select(n =>
        {
            var dto = MapToDto(n);
            if (n.Ticket != null)
            {
                dto.TicketTitle = n.Ticket.Title;
                dto.TicketCategory = n.Ticket.Category;
                dto.TicketBuilding = n.Ticket.Building;
                dto.TicketStatus = n.Ticket.Status.ToString();
            }
            return dto;
        }).ToList();
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null)
        {
            return false;
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            try
            {
                var unreadCount = await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
                await _hubContext.Clients.User(userId.ToString()).SendAsync("UpdateUnreadCount", unreadCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast updated unread count to user {UserId}", userId);
            }
        }

        return true;
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId)
    {
        var unreadNotifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        if (unreadNotifications.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        var affected = await _context.SaveChangesAsync();

        try
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync("UpdateUnreadCount", 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast updated unread count to user {UserId}", userId);
        }

        return affected;
    }

    public async Task<bool> DeleteNotificationAsync(Guid notificationId, Guid userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null)
        {
            return false;
        }

        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync();

        try
        {
            var unreadCount = await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            await _hubContext.Clients.User(userId.ToString()).SendAsync("UpdateUnreadCount", unreadCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast updated unread count to user {UserId}", userId);
        }

        return true;
    }

    public async Task<int> ClearAllNotificationsAsync(Guid userId)
    {
        var userNotifications = await _context.Notifications
            .Where(n => n.UserId == userId)
            .ToListAsync();

        if (userNotifications.Count == 0)
        {
            return 0;
        }

        _context.Notifications.RemoveRange(userNotifications);
        var removed = await _context.SaveChangesAsync();

        try
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync("UpdateUnreadCount", 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast updated unread count to user {UserId}", userId);
        }

        return removed;
    }

    private static NotificationDto MapToDto(Notification n)
    {
        return new NotificationDto
        {
            Id = n.Id,
            UserId = n.UserId,
            TicketId = n.TicketId,
            Title = n.Title,
            Message = n.Message,
            Type = n.Type,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt,
            ReadAt = n.ReadAt
        };
    }
}
