using FixMyCampus.Application.DTOs.Tickets;

namespace FixMyCampus.Application.DTOs.Dashboard;

public class AdminDashboardDto
{
    public int TotalTickets { get; set; }
    public int NewTickets { get; set; }
    public int AssignedTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int ResolvedTickets { get; set; }
    public IReadOnlyList<TicketDto> RecentTickets { get; set; } = new List<TicketDto>();
}

public class TechnicianDashboardDto
{
    public int AssignedTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int ResolvedTickets { get; set; }
    public IReadOnlyList<TicketDto> RecentTickets { get; set; } = new List<TicketDto>();
}
