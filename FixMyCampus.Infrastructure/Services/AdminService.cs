using FixMyCampus.Application.DTOs.Dashboard;
using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Application.DTOs.Users;
using FixMyCampus.Application.Interfaces;
using FixMyCampus.Domain.Entities;
using FixMyCampus.Domain.Enums;
using FixMyCampus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace FixMyCampus.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITicketService _ticketService;
    private readonly INotificationService _notificationService;

    public AdminService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ITicketService ticketService,
        INotificationService notificationService)
    {
        _context = context;
        _userManager = userManager;
        _ticketService = ticketService;
        _notificationService = notificationService;
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
        var activeTechs = technicians.Where(t => !t.IsDeleted).ToList();
        var techIds = activeTechs.Select(t => t.Id).ToList();

        var tickets = await _context.Tickets
            .Include(t => t.Assignments)
            .Where(t => !t.IsDeleted && t.Assignments.Any(a => techIds.Contains(a.TechnicianId) && a.UnassignedAt == null))
            .AsNoTracking()
            .ToListAsync();

        var openStatuses = new[] { TicketStatus.New, TicketStatus.Assigned, TicketStatus.InProgress };

        return activeTechs.Select(t =>
        {
            var techTickets = tickets.Where(tk => tk.Assignments.Any(a => a.TechnicianId == t.Id && a.UnassignedAt == null)).ToList();

            return new TechnicianDto
            {
                Id = t.Id,
                Name = !string.IsNullOrWhiteSpace(t.FullName) ? t.FullName : (!string.IsNullOrWhiteSpace($"{t.FirstName} {t.LastName}".Trim()) ? $"{t.FirstName} {t.LastName}".Trim() : (t.UserName ?? "Technician")),
                Email = t.Email ?? "",
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
        }).OrderByDescending(t => t.CreatedAt).ToList();
    }

    public async Task<PaginatedResult<TechnicianDto>> GetTechniciansAsync(TechnicianQueryParameters parameters)
    {
        var allTechs = await GetTechniciansAsync();
        var query = allTechs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var search = parameters.Search.Trim().ToLowerInvariant();
            query = query.Where(t =>
                (!string.IsNullOrEmpty(t.Name) && t.Name.ToLowerInvariant().Contains(search)) ||
                (!string.IsNullOrEmpty(t.Email) && t.Email.ToLowerInvariant().Contains(search)) ||
                (!string.IsNullOrEmpty(t.Phone) && t.Phone.ToLowerInvariant().Contains(search)) ||
                (!string.IsNullOrEmpty(t.Department) && t.Department.ToLowerInvariant().Contains(search)) ||
                (!string.IsNullOrEmpty(t.Specialty) && t.Specialty.ToLowerInvariant().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Status) && !parameters.Status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => string.Equals(t.Status, parameters.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Department) && !parameters.Department.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => string.Equals(t.Department, parameters.Department, StringComparison.OrdinalIgnoreCase));
        }

        query = parameters.SortBy?.ToLowerInvariant() switch
        {
            "name" => parameters.SortDescending ? query.OrderByDescending(t => t.Name) : query.OrderBy(t => t.Name),
            "email" => parameters.SortDescending ? query.OrderByDescending(t => t.Email) : query.OrderBy(t => t.Email),
            "status" => parameters.SortDescending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
            "department" => parameters.SortDescending ? query.OrderByDescending(t => t.Department) : query.OrderBy(t => t.Department),
            "specialty" => parameters.SortDescending ? query.OrderByDescending(t => t.Specialty) : query.OrderBy(t => t.Specialty),
            "tickets" or "activetickets" => parameters.SortDescending ? query.OrderByDescending(t => t.ActiveTicketsCount) : query.OrderBy(t => t.ActiveTicketsCount),
            _ => parameters.SortDescending ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt)
        };

        var totalCount = query.Count();
        var page = Math.Max(1, parameters.Page);
        var pageSize = Math.Clamp(parameters.PageSize, 1, 100);

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PaginatedResult<TechnicianDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<TechnicianStatsDto> GetTechnicianStatsAsync()
    {
        var allTechs = await GetTechniciansAsync();
        return new TechnicianStatsDto
        {
            TotalTechnicians = allTechs.Count,
            AvailableTechnicians = allTechs.Count(t => string.Equals(t.Status, "AVAILABLE", StringComparison.OrdinalIgnoreCase)),
            BusyTechnicians = allTechs.Count(t => string.Equals(t.Status, "BUSY", StringComparison.OrdinalIgnoreCase)),
            OnCallTechnicians = allTechs.Count(t => string.Equals(t.Status, "ON_CALL", StringComparison.OrdinalIgnoreCase)),
            OffDutyTechnicians = allTechs.Count(t => string.Equals(t.Status, "OFF_DUTY", StringComparison.OrdinalIgnoreCase))
        };
    }

    public async Task<TechnicianDetailDto> GetTechnicianByIdAsync(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.IsDeleted || !await _userManager.IsInRoleAsync(user, "Technician"))
        {
            throw new KeyNotFoundException($"Technician with ID {id} was not found.");
        }

        var tickets = await _context.Tickets
            .Include(t => t.Assignments)
            .Where(t => !t.IsDeleted && t.Assignments.Any(a => a.TechnicianId == id && a.UnassignedAt == null))
            .OrderByDescending(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var openStatuses = new[] { TicketStatus.New, TicketStatus.Assigned, TicketStatus.InProgress };

        return new TechnicianDetailDto
        {
            Id = user.Id,
            Name = user.FullName,
            Email = user.Email ?? "",
            Phone = user.PhoneNumber ?? "",
            Department = user.Department ?? "General",
            Specialty = user.Specialty ?? "General",
            Status = user.TechnicianStatus ?? "AVAILABLE",
            AssignedTicketCount = tickets.Count,
            ActiveTicketsCount = tickets.Count(tk => openStatuses.Contains(tk.Status)),
            ResolvedTicketsCount = tickets.Count(tk => tk.Status == TicketStatus.Resolved || tk.Status == TicketStatus.Closed),
            Rating = 5.0,
            CreatedAt = user.CreatedAt,
            AssignedTickets = tickets.Select(t => new ReporterTicketSummaryDto
            {
                Id = t.Id,
                Title = t.Title,
                Category = t.Category,
                Building = t.Building,
                Floor = t.Floor,
                Room = t.Room,
                Priority = t.Priority.ToString(),
                Status = t.Status.ToString(),
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }).ToList()
        };
    }

    public async Task<TechnicianDto> UpdateTechnicianAsync(Guid id, UpdateTechnicianDto request)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.IsDeleted || !await _userManager.IsInRoleAsync(user, "Technician"))
        {
            throw new KeyNotFoundException($"Technician with ID {id} was not found.");
        }

        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existingByEmail = await _userManager.FindByEmailAsync(request.Email);
            if (existingByEmail != null && existingByEmail.Id != id)
            {
                throw new InvalidOperationException($"Email '{request.Email}' is already registered.");
            }
            user.Email = request.Email;
            user.UserName = request.Email;
        }

        user.FullName = request.Name;
        user.PhoneNumber = request.Phone;
        user.Department = request.Department;
        user.Specialty = request.Specialty;
        user.TechnicianStatus = request.Status?.ToUpperInvariant() ?? "AVAILABLE";
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return (await GetTechniciansAsync()).First(t => t.Id == id);
    }

    public async Task DeleteTechnicianAsync(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.IsDeleted || !await _userManager.IsInRoleAsync(user, "Technician"))
        {
            throw new KeyNotFoundException($"Technician with ID {id} was not found.");
        }

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);
    }

    public async Task<TechnicianDto> UpdateTechnicianStatusAsync(Guid id, string status)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.IsDeleted || !await _userManager.IsInRoleAsync(user, "Technician"))
        {
            throw new KeyNotFoundException($"Technician with ID {id} was not found.");
        }

        var normalized = status.ToUpperInvariant();
        user.TechnicianStatus = normalized;
        user.UpdatedAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);
        return (await GetTechniciansAsync()).First(t => t.Id == id);
    }

    public async Task<TechnicianDto> CreateTechnicianAsync(CreateTechnicianDto request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        var nameParts = (request.Name ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts.Length > 0 ? nameParts[0] : request.Name;
        var lastName = nameParts.Length > 1 ? nameParts[1] : string.Empty;

        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FullName = request.Name.Trim(),
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = request.Phone?.Trim(),
            Department = request.Department?.Trim(),
            Specialty = request.Specialty?.Trim(),
            TechnicianStatus = request.Status?.ToUpperInvariant() ?? "AVAILABLE",
            Status = "ACTIVE",
            IsActive = true,
            IsDeleted = false,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var password = string.IsNullOrWhiteSpace(request.Password)
            ? "Tech" + RandomNumberGenerator.GetInt32(100, 999) + "!"
            : request.Password;

        var result = await _userManager.CreateAsync(user, password);
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
            Phone = user.PhoneNumber ?? "",
            Department = user.Department ?? "General",
            Specialty = user.Specialty ?? "General",
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
        var activeReporters = reporters.Where(r => !r.IsDeleted).ToList();
        var reporterIds = activeReporters.Select(r => r.Id).ToList();

        var tickets = await _context.Tickets
            .Where(t => reporterIds.Contains(t.ReporterId))
            .AsNoTracking()
            .ToListAsync();

        var openStatuses = new[] { TicketStatus.New, TicketStatus.Assigned, TicketStatus.InProgress };

        return activeReporters.Select(r =>
        {
            var parts = (r.FullName ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var fn = !string.IsNullOrWhiteSpace(r.FirstName) ? r.FirstName : (parts.Length > 0 ? parts[0] : string.Empty);
            var ln = !string.IsNullOrWhiteSpace(r.LastName) ? r.LastName : (parts.Length > 1 ? parts[1] : string.Empty);

            return new ReporterDto
            {
                Id = r.Id,
                Name = r.FullName,
                FirstName = fn,
                LastName = ln,
                Email = r.Email ?? "",
                Phone = r.PhoneNumber ?? "",
                Department = r.Department ?? "General",
                Role = "REPORTER",
                Status = !string.IsNullOrWhiteSpace(r.Status) ? r.Status : (r.IsActive ? "ACTIVE" : "DEACTIVATED"),
                IsActive = r.IsActive,
                IsDeleted = r.IsDeleted,
                DeletedAt = r.DeletedAt,
                SubmittedTicketsCount = tickets.Count(t => t.ReporterId == r.Id),
                OpenTicketsCount = tickets.Count(t => t.ReporterId == r.Id && openStatuses.Contains(t.Status)),
                LastActiveAt = r.UpdatedAt,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            };
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

            try
            {
                await _notificationService.CreateAndSendNotificationAsync(
                    technicianId,
                    "New Issue Assigned",
                    $"You have been assigned a new issue: \"{ticket.Title}\".",
                    "IssueAssigned",
                    ticketId);
            }
            catch
            {
                // Notification failure must not fail the assignment operation
            }

            var tickets = await _ticketService.GetTicketsAsync(null);
            return tickets.First(t => t.Id == ticketId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteTicketAsync(Guid ticketId, Guid adminId)
    {
        await _ticketService.DeleteTicketAsync(ticketId, adminId);
    }
}
