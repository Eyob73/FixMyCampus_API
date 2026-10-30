namespace FixMyCampus.Application.DTOs.Notifications;

public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? TicketId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public bool Read => IsRead;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    
    public string? TicketTitle { get; set; }
    public string? TicketCategory { get; set; }
    public string? TicketBuilding { get; set; }
    public string? TicketStatus { get; set; }
}
