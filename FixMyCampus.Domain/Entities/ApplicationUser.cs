using Microsoft.AspNetCore.Identity;

namespace FixMyCampus.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Ticket> ReportedTickets { get; set; } = new List<Ticket>();

    public ICollection<TicketAssignment> TechnicianAssignments { get; set; } = new List<TicketAssignment>();

    public ICollection<TicketAssignment> CreatedAssignments { get; set; } = new List<TicketAssignment>();

    public ICollection<TicketHistory> TicketHistories { get; set; } = new List<TicketHistory>();

    // Technician fields
    public string? Department { get; set; }
    public string? Specialty { get; set; }
    public string? TechnicianStatus { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<TicketImage> UploadedImages { get; set; } = new List<TicketImage>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
