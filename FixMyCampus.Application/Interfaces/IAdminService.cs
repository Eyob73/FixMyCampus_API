using FixMyCampus.Application.DTOs.Dashboard;
using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Application.DTOs.Users;

namespace FixMyCampus.Application.Interfaces;

public interface IAdminService
{
    Task<AdminDashboardDto> GetDashboardAsync();
    Task<IReadOnlyList<TicketDto>> GetTicketsAsync(TicketFilterRequest? filter);
    Task<IReadOnlyList<TechnicianDto>> GetTechniciansAsync();
    Task<PaginatedResult<TechnicianDto>> GetTechniciansAsync(TechnicianQueryParameters parameters);
    Task<TechnicianStatsDto> GetTechnicianStatsAsync();
    Task<TechnicianDetailDto> GetTechnicianByIdAsync(Guid id);
    Task<TechnicianDto> CreateTechnicianAsync(CreateTechnicianDto request);
    Task<TechnicianDto> UpdateTechnicianAsync(Guid id, UpdateTechnicianDto request);
    Task DeleteTechnicianAsync(Guid id);
    Task<TechnicianDto> UpdateTechnicianStatusAsync(Guid id, string status);
    Task<IReadOnlyList<ReporterDto>> GetReportersAsync();
    Task<TicketDto> AssignTechnicianAsync(Guid ticketId, Guid technicianId, Guid adminId);
    Task DeleteTicketAsync(Guid ticketId, Guid adminId);
}
