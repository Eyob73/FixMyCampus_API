using FixMyCampus.Domain.Enums;

namespace FixMyCampus.Domain.Entities;

public class TicketHistory
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public TicketStatus? OldStatus { get; set; }
    public TicketStatus NewStatus { get; set; }
    public Guid ChangedById { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? Note { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public ApplicationUser ChangedBy { get; set; } = null!;
}
