using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Application.DTOs.Users;
using FixMyCampus.Application.Interfaces;
using FixMyCampus.Domain.Entities;
using FixMyCampus.Domain.Enums;
using FixMyCampus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FixMyCampus.Infrastructure.Services;

public class TicketService : ITicketService
{
    private readonly ApplicationDbContext _context;

    public TicketService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TicketDto> CreateTicketAsync(Guid reporterId, CreateTicketRequest request)
    {
        var reporter = await _context.Users.FindAsync(reporterId) 
            ?? throw new Exception("Reporter not found");

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            ReporterId = reporterId,
            Category = request.Category,
            Building = request.Building,
            Room = request.Room,
            Description = request.Description,
            Status = TicketStatus.New,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var history = new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            OldStatus = null,
            NewStatus = TicketStatus.New,
            ChangedById = reporterId,
            ChangedAt = DateTime.UtcNow
        };

        _context.Tickets.Add(ticket);
        _context.TicketHistories.Add(history);
        await _context.SaveChangesAsync();

        return await MapToDtoAsync(ticket);
    }

    public async Task<IReadOnlyList<TicketDto>> GetTicketsAsync(TicketFilterRequest? filter)
    {
        var query = _context.Tickets
            .Include(t => t.Reporter)
            .Include(t => t.Assignments)
                .ThenInclude(a => a.Technician)
            .AsNoTracking()
            .AsQueryable();

        if (filter != null)
        {
            if (filter.Status.HasValue) query = query.Where(t => t.Status == filter.Status.Value);
            if (!string.IsNullOrEmpty(filter.Category)) query = query.Where(t => t.Category.Contains(filter.Category));
            if (!string.IsNullOrEmpty(filter.Building)) query = query.Where(t => t.Building.Contains(filter.Building));
        }

        var tickets = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
        return tickets.Select(MapToDtoSync).ToList();
    }

    public async Task<IReadOnlyList<TicketDto>> GetMyTicketsAsync(Guid reporterId)
    {
        var query = _context.Tickets
            .Include(t => t.Reporter)
            .Include(t => t.Assignments)
                .ThenInclude(a => a.Technician)
            .Where(t => t.ReporterId == reporterId)
            .AsNoTracking();

        var tickets = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
        return tickets.Select(MapToDtoSync).ToList();
    }

    public async Task<TicketDetailsDto> GetTicketByIdAsync(Guid ticketId, Guid currentUserId, bool isAdmin)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Reporter)
            .Include(t => t.Assignments)
                .ThenInclude(a => a.Technician)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket == null) throw new KeyNotFoundException("Ticket not found");

        if (!isAdmin && ticket.ReporterId != currentUserId)
        {
            var isTechnician = ticket.Assignments.Any(a => a.TechnicianId == currentUserId && a.UnassignedAt == null);
            if (!isTechnician) throw new UnauthorizedAccessException("Not authorized to view this ticket");
        }

        var dto = MapToDtoSync(ticket);
        return new TicketDetailsDto
        {
            Id = dto.Id,
            Category = dto.Category,
            Building = dto.Building,
            Room = dto.Room,
            Description = dto.Description,
            Status = dto.Status,
            Reporter = dto.Reporter,
            AssignedTechnician = dto.AssignedTechnician,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = ticket.UpdatedAt
        };
    }

    public async Task<IReadOnlyList<TicketHistoryDto>> GetTicketHistoryAsync(Guid ticketId, Guid currentUserId, bool isAdmin)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Assignments)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket == null) throw new KeyNotFoundException("Ticket not found");

        if (!isAdmin && ticket.ReporterId != currentUserId)
        {
            var isTechnician = ticket.Assignments.Any(a => a.TechnicianId == currentUserId && a.UnassignedAt == null);
            if (!isTechnician) throw new UnauthorizedAccessException("Not authorized to view this ticket history");
        }

        var histories = await _context.TicketHistories
            .Include(h => h.ChangedBy)
            .Where(h => h.TicketId == ticketId)
            .OrderByDescending(h => h.ChangedAt)
            .AsNoTracking()
            .ToListAsync();

        return histories.Select(h => new TicketHistoryDto
        {
            Id = h.Id,
            TicketId = h.TicketId,
            OldStatus = h.OldStatus?.ToString(),
            NewStatus = h.NewStatus.ToString(),
            ChangedAt = h.ChangedAt,
            ChangedBy = new UserDto
            {
                Id = h.ChangedBy.Id,
                FullName = h.ChangedBy.FullName,
                Email = h.ChangedBy.Email!
            }
        }).ToList();
    }

    private async Task<TicketDto> MapToDtoAsync(Ticket ticket)
    {
        if (ticket.Reporter == null)
        {
            await _context.Entry(ticket).Reference(t => t.Reporter).LoadAsync();
        }
        
        await _context.Entry(ticket).Collection(t => t.Assignments).Query().Include(a => a.Technician).LoadAsync();
        
        return MapToDtoSync(ticket);
    }

    private TicketDto MapToDtoSync(Ticket ticket)
    {
        var activeAssignment = ticket.Assignments?.FirstOrDefault(a => a.UnassignedAt == null);
        return new TicketDto
        {
            Id = ticket.Id,
            Category = ticket.Category,
            Building = ticket.Building,
            Room = ticket.Room,
            Description = ticket.Description,
            Status = ticket.Status.ToString(),
            CreatedAt = ticket.CreatedAt,
            Reporter = new UserDto
            {
                Id = ticket.Reporter.Id,
                FullName = ticket.Reporter.FullName,
                Email = ticket.Reporter.Email!
            },
            AssignedTechnician = activeAssignment != null ? new UserDto
            {
                Id = activeAssignment.Technician.Id,
                FullName = activeAssignment.Technician.FullName,
                Email = activeAssignment.Technician.Email!
            } : null
        };
    }
}
