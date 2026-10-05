using FixMyCampus.Application.DTOs.Dashboard;
using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Application.DTOs.Users;

namespace FixMyCampus.Application.Interfaces;

public interface IAdminService
{
    Task<AdminDashboardDto> GetDashboardAsync();
    Task<IReadOnlyList<TicketDto>> GetTicketsAsync(TicketFilterRequest? filter);
    Task<IReadOnlyList<UserDto>> GetTechniciansAsync();
    Task<TicketDto> AssignTechnicianAsync(Guid ticketId, Guid technicianId, Guid adminId);
}
