using FixMyCampus.Application.DTOs.Tickets;

namespace FixMyCampus.Application.DTOs.Dashboard;

public class AdminDashboardDto
{
    public int TotalTickets { get; set; }
    public int NewTickets { get; set; }
    public int AssignedTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int ResolvedTickets { get; set; }
    public int ClosedTickets { get; set; }
    public int UnassignedTickets { get; set; }
    public IReadOnlyList<TicketDto> RecentTickets { get; set; } = new List<TicketDto>();
}

public class TechnicianDashboardDto
{
    public int TotalAssigned { get; set; }
    public int NewAssigned { get; set; }
    public int InProgress { get; set; }
    public int Resolved { get; set; }
    public int Closed { get; set; }
    public int HighPriority { get; set; }
    public PriorityCountsDto PriorityCounts { get; set; } = new();
    public IReadOnlyList<TicketDto> RecentTickets { get; set; } = new List<TicketDto>();
}

public class PriorityCountsDto
{
    public int Critical { get; set; }
    public int High { get; set; }
    public int Medium { get; set; }
    public int Low { get; set; }
}

public class ReporterDashboardDto
{
    public int TotalTickets { get; set; }
    public int NewTickets { get; set; }
    public int AssignedTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int ResolvedTickets { get; set; }
}
