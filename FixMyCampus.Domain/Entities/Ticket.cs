using FixMyCampus.Domain.Enums;

namespace FixMyCampus.Domain.Entities;

public class Ticket
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Building { get; set; } = string.Empty;
    public string? Room { get; set; }
    public string Description { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketStatus Status { get; set; }
    public Guid ReporterId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ApplicationUser Reporter { get; set; } = null!;
    
    public ICollection<TicketAssignment> Assignments { get; set; } = new List<TicketAssignment>();
    public ICollection<TicketHistory> Histories { get; set; } = new List<TicketHistory>();
    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
    
    public List<string> InternalNotes { get; set; } = new List<string>();
}
