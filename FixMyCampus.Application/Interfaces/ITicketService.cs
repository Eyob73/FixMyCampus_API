using FixMyCampus.Application.DTOs.Tickets;

namespace FixMyCampus.Application.Interfaces;

public interface ITicketService
{
    Task<TicketDto> CreateTicketAsync(Guid reporterId, CreateTicketRequest request, IEnumerable<Microsoft.AspNetCore.Http.IFormFile>? files = null);
    Task<IReadOnlyList<TicketDto>> GetTicketsAsync(TicketFilterRequest? filter);
    Task<IReadOnlyList<TicketDto>> GetMyTicketsAsync(Guid reporterId, TicketFilterRequest? filter = null);
    Task<TicketDetailsDto> GetTicketByIdAsync(Guid ticketId, Guid currentUserId, bool isAdmin);
    Task<IReadOnlyList<TicketHistoryDto>> GetTicketHistoryAsync(Guid ticketId, Guid currentUserId, bool isAdmin);
    Task<TicketDetailsDto> AddInternalNoteAsync(Guid ticketId, Guid currentUserId, bool isAdmin, AddNoteRequest request);
    Task<TicketCommentDto> AddCommentAsync(Guid ticketId, Guid currentUserId, bool isAdmin, AddCommentRequest request);
    Task<FixMyCampus.Application.DTOs.Dashboard.ReporterDashboardDto> GetReporterDashboardAsync(Guid reporterId);
    Task<FixMyCampus.Application.DTOs.Dashboard.AdminDashboardDto> GetAdminDashboardAsync();
    Task<TicketDetailsDto> UpdatePriorityAsync(Guid ticketId, Guid currentUserId, bool isAdmin, FixMyCampus.Domain.Enums.TicketPriority newPriority);
    Task<TicketDetailsDto> UpdateStatusAsync(Guid ticketId, Guid currentUserId, bool isAdmin, FixMyCampus.Domain.Enums.TicketStatus newStatus, string? note);
    Task<List<TicketImageDto>> AddTicketImagesAsync(Guid ticketId, Guid currentUserId, bool isAdmin, FixMyCampus.Domain.Enums.ImageCategory category, IEnumerable<Microsoft.AspNetCore.Http.IFormFile> files);
    Task<(Stream Stream, string ContentType, string FileName)> GetTicketImageStreamAsync(Guid ticketId, Guid imageId);
    Task<DuplicateCheckResponse> CheckDuplicateTicketAsync(DuplicateCheckRequest request);
    Task DeleteTicketAsync(Guid ticketId, Guid currentUserId);
}
