namespace FixMyCampus.Domain.Entities;

public class TicketAssignment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid TechnicianId { get; set; }
    public Guid AssignedById { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public ApplicationUser Technician { get; set; } = null!;
    public ApplicationUser AssignedBy { get; set; } = null!;
}
