using FixMyCampus.Domain.Enums;

namespace FixMyCampus.Domain.Entities;

public class TicketImage
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public ImageCategory Category { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public Guid UploaderId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Ticket Ticket { get; set; } = null!;
    public ApplicationUser Uploader { get; set; } = null!;
}
