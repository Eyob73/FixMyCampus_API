using FixMyCampus.Application.DTOs.Dashboard;
using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Application.DTOs.Users;
using FixMyCampus.Application.Interfaces;
using FixMyCampus.Domain.Entities;
using FixMyCampus.Domain.Enums;
using FixMyCampus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FixMyCampus.Infrastructure.Services;

public class TechnicianService : ITechnicianService
{
    private readonly ApplicationDbContext _context;
    private readonly ITicketService _ticketService;

    public TechnicianService(ApplicationDbContext context, ITicketService ticketService)
    {
        _context = context;
        _ticketService = ticketService;
    }

    public async Task<TechnicianDashboardDto> GetDashboardAsync(Guid technicianId)
    {
        var assigned = await _context.TicketAssignments
            .Where(a => a.TechnicianId == technicianId && a.UnassignedAt == null)
            .Select(a => a.Ticket)
            .CountAsync(t => t.Status == TicketStatus.Assigned);

        var inProgress = await _context.TicketAssignments
            .Where(a => a.TechnicianId == technicianId && a.UnassignedAt == null)
            .Select(a => a.Ticket)
            .CountAsync(t => t.Status == TicketStatus.InProgress);

        var resolved = await _context.TicketHistories
            .Where(h => h.ChangedById == technicianId && h.NewStatus == TicketStatus.Resolved)
            .CountAsync(); // Tickets resolved by this tech

        var recent = await GetAssignedTicketsAsync(technicianId, null);

        return new TechnicianDashboardDto
        {
            AssignedTickets = assigned,
            InProgressTickets = inProgress,
            ResolvedTickets = resolved,
            RecentTickets = recent.Take(10).ToList()
        };
    }

    public async Task<IReadOnlyList<TicketDto>> GetAssignedTicketsAsync(Guid technicianId, TicketFilterRequest? filter)
    {
        var query = _context.Tickets
            .Include(t => t.Reporter)
            .Include(t => t.Assignments)
                .ThenInclude(a => a.Technician)
            .Where(t => t.Assignments.Any(a => a.TechnicianId == technicianId && a.UnassignedAt == null))
            .AsNoTracking()
            .AsQueryable();

        if (filter != null)
        {
            if (filter.Status.HasValue) query = query.Where(t => t.Status == filter.Status.Value);
            if (!string.IsNullOrEmpty(filter.Category)) query = query.Where(t => t.Category.Contains(filter.Category));
            if (!string.IsNullOrEmpty(filter.Building)) query = query.Where(t => t.Building.Contains(filter.Building));
        }

        var tickets = await query.OrderByDescending(t => t.UpdatedAt).ToListAsync();
        
        return tickets.Select(MapToDtoSync).ToList();
    }

    public async Task<TicketDto> StartWorkAsync(Guid ticketId, Guid technicianId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var ticket = await _context.Tickets
                .Include(t => t.Assignments)
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket == null) throw new KeyNotFoundException("Ticket not found");

            var isAssigned = ticket.Assignments.Any(a => a.TechnicianId == technicianId && a.UnassignedAt == null);
            if (!isAssigned) throw new UnauthorizedAccessException("User is not the assigned technician");

            if (ticket.Status != TicketStatus.Assigned)
                throw new InvalidOperationException("Ticket is not in Assigned status");

            var history = new TicketHistory
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                OldStatus = ticket.Status,
                NewStatus = TicketStatus.InProgress,
                ChangedById = technicianId,
                ChangedAt = DateTime.UtcNow
            };

            ticket.Status = TicketStatus.InProgress;
            ticket.UpdatedAt = DateTime.UtcNow;

            _context.TicketHistories.Add(history);
            _context.Tickets.Update(ticket);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var assigned = await GetAssignedTicketsAsync(technicianId, null);
            return assigned.First(t => t.Id == ticketId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<TicketDto> ResolveTicketAsync(Guid ticketId, Guid technicianId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var ticket = await _context.Tickets
                .Include(t => t.Assignments)
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket == null) throw new KeyNotFoundException("Ticket not found");

            var isAssigned = ticket.Assignments.Any(a => a.TechnicianId == technicianId && a.UnassignedAt == null);
            if (!isAssigned) throw new UnauthorizedAccessException("User is not the assigned technician");

            if (ticket.Status != TicketStatus.InProgress)
                throw new InvalidOperationException("Ticket is not in InProgress status");

            var history = new TicketHistory
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                OldStatus = ticket.Status,
                NewStatus = TicketStatus.Resolved,
                ChangedById = technicianId,
                ChangedAt = DateTime.UtcNow
            };

            ticket.Status = TicketStatus.Resolved;
            ticket.UpdatedAt = DateTime.UtcNow;

            _context.TicketHistories.Add(history);
            _context.Tickets.Update(ticket);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var assigned = await GetAssignedTicketsAsync(technicianId, null);
            return assigned.First(t => t.Id == ticketId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private FixMyCampus.Application.DTOs.Tickets.TicketDto MapToDtoSync(Ticket ticket)
    {
        var activeAssignment = ticket.Assignments?.FirstOrDefault(a => a.UnassignedAt == null);
        return new FixMyCampus.Application.DTOs.Tickets.TicketDto
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
