using FixMyCampus.Application.DTOs.Dashboard;
using FixMyCampus.Application.DTOs.Tickets;

namespace FixMyCampus.Application.Interfaces;

public interface ITechnicianService
{
    Task<TechnicianDashboardDto> GetDashboardAsync(Guid technicianId);
    Task<IReadOnlyList<TicketDto>> GetAssignedTicketsAsync(Guid technicianId, TicketFilterRequest? filter);
    Task<TicketDto> StartWorkAsync(Guid ticketId, Guid technicianId);
    Task<TicketDto> ResolveTicketAsync(Guid ticketId, Guid technicianId, ResolveTicketRequest request);
}
