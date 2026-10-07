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
        var closed = await _context.Tickets.CountAsync(t => t.Status == TicketStatus.Closed);

        var recent = await _ticketService.GetTicketsAsync(null); // Could optimize to take top 10

        return new AdminDashboardDto
        {
            TotalTickets = total,
            NewTickets = @new,
            AssignedTickets = assigned,
            InProgressTickets = inProgress,
            ResolvedTickets = resolved,
            ClosedTickets = closed,
            UnassignedTickets = @new,
            RecentTickets = recent.Take(10).ToList()
        };
    }

    public async Task<IReadOnlyList<TicketDto>> GetTicketsAsync(TicketFilterRequest? filter)
    {
        return await _ticketService.GetTicketsAsync(filter);
    }

    public async Task<IReadOnlyList<TechnicianDto>> GetTechniciansAsync()
    {
        var technicians = await _userManager.GetUsersInRoleAsync("Technician");
        var techIds = technicians.Select(t => t.Id).ToList();

        var tickets = await _context.Tickets
            .Include(t => t.Assignments)
            .Where(t => t.Assignments.Any(a => techIds.Contains(a.TechnicianId) && a.UnassignedAt == null))
            .AsNoTracking()
            .ToListAsync();

        var openStatuses = new[] { TicketStatus.New, TicketStatus.Assigned, TicketStatus.InProgress };

        return technicians.Select(t =>
        {
            var techTickets = tickets.Where(tk => tk.Assignments.Any(a => a.TechnicianId == t.Id && a.UnassignedAt == null)).ToList();

            return new TechnicianDto
            {
                Id = t.Id,
                Name = t.FullName,
                Email = t.Email!,
                Phone = t.PhoneNumber ?? "",
                Department = t.Department ?? "General",
                Specialty = t.Specialty ?? "General",
                Status = t.TechnicianStatus ?? "AVAILABLE",
                AssignedTicketCount = techTickets.Count,
                ActiveTicketsCount = techTickets.Count(tk => openStatuses.Contains(tk.Status)),
                ResolvedTicketsCount = techTickets.Count(tk => tk.Status == TicketStatus.Resolved || tk.Status == TicketStatus.Closed),
                Rating = 5.0,
                CreatedAt = t.CreatedAt
            };
        }).ToList();
    }

    public async Task<TechnicianDto> CreateTechnicianAsync(CreateTechnicianDto request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.Name,
            PhoneNumber = request.Phone,
            Department = request.Department,
            Specialty = request.Specialty,
            TechnicianStatus = request.Status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, "Password123!");
        if (!result.Succeeded)
        {
            throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        await _userManager.AddToRoleAsync(user, "Technician");

        return new TechnicianDto
        {
            Id = user.Id,
            Name = user.FullName,
            Email = user.Email,
            Phone = user.PhoneNumber,
            Department = user.Department,
            Specialty = user.Specialty,
            Status = user.TechnicianStatus,
            AssignedTicketCount = 0,
            ActiveTicketsCount = 0,
            ResolvedTicketsCount = 0,
            Rating = 5.0,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<IReadOnlyList<ReporterDto>> GetReportersAsync()
    {
        var reporters = await _userManager.GetUsersInRoleAsync("Reporter");
        var reporterIds = reporters.Select(r => r.Id).ToList();

        var tickets = await _context.Tickets
            .Where(t => reporterIds.Contains(t.ReporterId))
            .AsNoTracking()
            .ToListAsync();

        var openStatuses = new[] { TicketStatus.New, TicketStatus.Assigned, TicketStatus.InProgress };

        return reporters.Select(r => new ReporterDto
        {
            Id = r.Id,
            Name = r.FullName,
            Email = r.Email ?? "",
            Phone = "",
            Department = "General", // Placeholder
            Role = "REPORTER",
            Status = "ACTIVE",
            SubmittedTicketsCount = tickets.Count(t => t.ReporterId == r.Id),
            OpenTicketsCount = tickets.Count(t => t.ReporterId == r.Id && openStatuses.Contains(t.Status)),
            LastActiveAt = DateTime.UtcNow,
            CreatedAt = r.CreatedAt
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

            if (ticket.Status != TicketStatus.New && ticket.Status != TicketStatus.Assigned)
                throw new InvalidOperationException("Can only assign technicians to New or Assigned tickets");

            var technician = await _userManager.FindByIdAsync(technicianId.ToString());
            if (technician == null) throw new KeyNotFoundException("Technician not found");

            if (!await _userManager.IsInRoleAsync(technician, "Technician"))
                throw new InvalidOperationException("User is not a technician");

            var existingAssignment = await _context.TicketAssignments
                .FirstOrDefaultAsync(a => a.TicketId == ticketId && a.UnassignedAt == null);

            if (existingAssignment != null)
            {
                existingAssignment.UnassignedAt = DateTime.UtcNow;
                _context.TicketAssignments.Update(existingAssignment);
            }

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
