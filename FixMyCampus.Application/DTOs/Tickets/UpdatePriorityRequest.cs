using FixMyCampus.Domain.Enums;

namespace FixMyCampus.Application.DTOs.Tickets;

public class UpdatePriorityRequest
{
    public TicketPriority Priority { get; set; }
}
