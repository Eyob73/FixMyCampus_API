using FixMyCampus.Application.DTOs.Tickets;

namespace FixMyCampus.Application.Interfaces;

public interface ITicketService
{
    Task<TicketDto> CreateTicketAsync(Guid reporterId, CreateTicketRequest request);
    Task<IReadOnlyList<TicketDto>> GetTicketsAsync(TicketFilterRequest? filter);
    Task<IReadOnlyList<TicketDto>> GetMyTicketsAsync(Guid reporterId);
    Task<TicketDetailsDto> GetTicketByIdAsync(Guid ticketId, Guid currentUserId, bool isAdmin);
    Task<IReadOnlyList<TicketHistoryDto>> GetTicketHistoryAsync(Guid ticketId, Guid currentUserId, bool isAdmin);
}
