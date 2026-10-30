namespace FixMyCampus.Application.DTOs.Notifications;

public class NotificationFilterRequest
{
    public bool? UnreadOnly { get; set; }
    public string? Type { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
