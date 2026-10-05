using FixMyCampus.Application.DTOs.Dashboard;
using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Application.DTOs.Users;
using FixMyCampus.Application.Interfaces;
using FixMyCampus.Domain.Entities;
using FixMyCampus.Domain.Enums;
using FixMyCampus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FixMyCampus.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITicketService _ticketService;

    public AdminService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ITicketService ticketService)
    {
        _context = context;
        _userManager = userManager;
        _ticketService = ticketService;
    }

    public async Task<AdminDashboardDto> GetDashboardAsync()
    {
        var total = await _context.Tickets.CountAsync();
        var @new = await _context.Tickets.CountAsync(t => t.Status == TicketStatus.New);
        var assigned = await _context.Tickets.CountAsync(t => t.Status == TicketStatus.Assigned);
        var inProgress = await _context.Tickets.CountAsync(t => t.Status == TicketStatus.InProgress);
        var resolved = await _context.Tickets.CountAsync(t => t.Status == TicketStatus.Resolved);

        var recent = await _ticketService.GetTicketsAsync(null); // Could optimize to take top 10

        return new AdminDashboardDto
        {
            TotalTickets = total,
            NewTickets = @new,
            AssignedTickets = assigned,
            InProgressTickets = inProgress,
            ResolvedTickets = resolved,
            RecentTickets = recent.Take(10).ToList()
        };
    }

    public async Task<IReadOnlyList<TicketDto>> GetTicketsAsync(TicketFilterRequest? filter)
    {
        return await _ticketService.GetTicketsAsync(filter);
    }

    public async Task<IReadOnlyList<UserDto>> GetTechniciansAsync()
    {
        var technicians = await _userManager.GetUsersInRoleAsync("Technician");
        return technicians.Select(t => new UserDto
        {
            Id = t.Id,
            FullName = t.FullName,
            Email = t.Email!
        }).ToList();
    }

    public async Task<TicketDto> AssignTechnicianAsync(Guid ticketId, Guid technicianId, Guid adminId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var ticket = await _context.Tickets
                .Include(t => t.Reporter)
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket == null) throw new KeyNotFoundException("Ticket not found");

            if (ticket.Status != TicketStatus.New)
                throw new InvalidOperationException("Can only assign technicians to New tickets");

            var technician = await _userManager.FindByIdAsync(technicianId.ToString());
            if (technician == null) throw new KeyNotFoundException("Technician not found");

            if (!await _userManager.IsInRoleAsync(technician, "Technician"))
                throw new InvalidOperationException("User is not a technician");

            var assignment = new TicketAssignment
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                TechnicianId = technicianId,
                AssignedById = adminId,
                AssignedAt = DateTime.UtcNow
            };

            var history = new TicketHistory
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                OldStatus = ticket.Status,
                NewStatus = TicketStatus.Assigned,
                ChangedById = adminId,
                ChangedAt = DateTime.UtcNow
            };

            ticket.Status = TicketStatus.Assigned;
            ticket.UpdatedAt = DateTime.UtcNow;

            _context.TicketAssignments.Add(assignment);
            _context.TicketHistories.Add(history);
            _context.Tickets.Update(ticket);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var tickets = await _ticketService.GetTicketsAsync(null);
            return tickets.First(t => t.Id == ticketId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
