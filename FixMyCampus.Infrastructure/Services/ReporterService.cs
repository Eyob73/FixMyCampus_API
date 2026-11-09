using System.Security.Cryptography;
using FixMyCampus.Application.DTOs.Users;
using FixMyCampus.Application.Interfaces;
using FixMyCampus.Domain.Entities;
using FixMyCampus.Domain.Enums;
using FixMyCampus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FixMyCampus.Infrastructure.Services;

public class ReporterService : IReporterService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReporterService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<PaginatedResult<ReporterDto>> GetReportersAsync(ReporterQueryParameters parameters)
    {
        // 1. Get all users in Reporter role who are not soft deleted
        var reporters = await _userManager.GetUsersInRoleAsync("Reporter");
        var activeReporters = reporters.Where(u => !u.IsDeleted).ToList();

        // 2. Fetch ticket counts for all active reporters in a single batch query
        var reporterIds = activeReporters.Select(u => u.Id).ToList();
        var tickets = await _context.Tickets
            .Where(t => reporterIds.Contains(t.ReporterId))
            .Select(t => new { t.ReporterId, t.Status })
            .AsNoTracking()
            .ToListAsync();

        var openStatuses = new[] { TicketStatus.New, TicketStatus.Assigned, TicketStatus.InProgress };

        var ticketSummary = tickets
            .GroupBy(t => t.ReporterId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Total = g.Count(),
                    Open = g.Count(t => openStatuses.Contains(t.Status))
                });

        // 3. Map to DTOs
        var dtoList = activeReporters.Select(r =>
        {
            ticketSummary.TryGetValue(r.Id, out var counts);
            var (fn, ln) = ResolveNames(r);

            return new ReporterDto
            {
                Id = r.Id,
                Name = r.FullName,
                FirstName = fn,
                LastName = ln,
                Email = r.Email ?? string.Empty,
                Phone = r.PhoneNumber ?? string.Empty,
                Department = r.Department ?? string.Empty,
                Role = "REPORTER",
                Status = NormalizeStatus(r.Status, r.IsActive),
                IsActive = r.IsActive,
                IsDeleted = r.IsDeleted,
                DeletedAt = r.DeletedAt,
                SubmittedTicketsCount = counts?.Total ?? 0,
                OpenTicketsCount = counts?.Open ?? 0,
                LastActiveAt = r.UpdatedAt,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            };
        }).AsQueryable();

        // 4. Search Filter
        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var search = parameters.Search.Trim().ToLowerInvariant();
            dtoList = dtoList.Where(r =>
                r.Name.ToLowerInvariant().Contains(search) ||
                r.Email.ToLowerInvariant().Contains(search) ||
                r.Phone.ToLowerInvariant().Contains(search) ||
                r.Department.ToLowerInvariant().Contains(search));
        }

        // 5. Status Filter
        if (!string.IsNullOrWhiteSpace(parameters.Status) &&
            !parameters.Status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var statusFilter = parameters.Status.ToUpperInvariant();
            dtoList = dtoList.Where(r => r.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase));
        }

        // 6. Sorting
        var isDesc = parameters.SortDescending;
        dtoList = (parameters.SortBy?.ToLowerInvariant()) switch
        {
            "name" => isDesc ? dtoList.OrderByDescending(r => r.Name) : dtoList.OrderBy(r => r.Name),
            "email" => isDesc ? dtoList.OrderByDescending(r => r.Email) : dtoList.OrderBy(r => r.Email),
            "status" => isDesc ? dtoList.OrderByDescending(r => r.Status) : dtoList.OrderBy(r => r.Status),
            "tickets" or "submittedticketscount" => isDesc ? dtoList.OrderByDescending(r => r.SubmittedTicketsCount) : dtoList.OrderBy(r => r.SubmittedTicketsCount),
            _ => isDesc ? dtoList.OrderByDescending(r => r.CreatedAt) : dtoList.OrderBy(r => r.CreatedAt)
        };

        var totalCount = dtoList.Count();
        var page = parameters.Page < 1 ? 1 : parameters.Page;
        var pageSize = parameters.PageSize < 1 ? 10 : (parameters.PageSize > 100 ? 100 : parameters.PageSize);

        var items = dtoList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PaginatedResult<ReporterDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<ReporterDto>> GetAllReportersAsync()
    {
        var result = await GetReportersAsync(new ReporterQueryParameters
        {
            Page = 1,
            PageSize = 1000,
            SortDescending = true
        });
        return result.Items;
    }

    public async Task<ReporterStatsDto> GetStatsAsync()
    {
        var reporters = await _userManager.GetUsersInRoleAsync("Reporter");
        var activeReporters = reporters.Where(u => !u.IsDeleted).ToList();

        return new ReporterStatsDto
        {
            TotalReporters = activeReporters.Count,
            ActiveReporters = activeReporters.Count(r => NormalizeStatus(r.Status, r.IsActive) == "ACTIVE"),
            SuspendedReporters = activeReporters.Count(r => NormalizeStatus(r.Status, r.IsActive) == "SUSPENDED"),
            DeactivatedReporters = activeReporters.Count(r => NormalizeStatus(r.Status, r.IsActive) == "DEACTIVATED")
        };
    }

    public async Task<ReporterDetailDto> GetReporterByIdAsync(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.IsDeleted)
        {
            throw new KeyNotFoundException($"Reporter with ID '{id}' was not found.");
        }

        if (!await _userManager.IsInRoleAsync(user, "Reporter"))
        {
            throw new KeyNotFoundException($"User with ID '{id}' is not a reporter.");
        }

        var tickets = await _context.Tickets
            .Where(t => t.ReporterId == id)
            .OrderByDescending(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var (fn, ln) = ResolveNames(user);
        var status = NormalizeStatus(user.Status, user.IsActive);

        var ticketDtos = tickets.Select(t => new ReporterTicketSummaryDto
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
        }).ToList();

        return new ReporterDetailDto
        {
            Id = user.Id,
            Name = user.FullName,
            FirstName = fn,
            LastName = ln,
            Email = user.Email ?? string.Empty,
            Phone = user.PhoneNumber ?? string.Empty,
            Department = user.Department ?? string.Empty,
            Role = "REPORTER",
            Status = status,
            IsActive = user.IsActive,
            IsDeleted = user.IsDeleted,
            DeletedAt = user.DeletedAt,
            SubmittedTicketsCount = tickets.Count,
            OpenTicketsCount = tickets.Count(t => t.Status == TicketStatus.New || t.Status == TicketStatus.Assigned || t.Status == TicketStatus.InProgress),
            NewTicketsCount = tickets.Count(t => t.Status == TicketStatus.New),
            AssignedTicketsCount = tickets.Count(t => t.Status == TicketStatus.Assigned),
            InProgressTicketsCount = tickets.Count(t => t.Status == TicketStatus.InProgress),
            ResolvedTicketsCount = tickets.Count(t => t.Status == TicketStatus.Resolved),
            ClosedTicketsCount = tickets.Count(t => t.Status == TicketStatus.Closed),
            Tickets = ticketDtos,
            LastActiveAt = user.UpdatedAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public async Task<ReporterDto> CreateReporterAsync(CreateReporterDto request)
    {
        // 1. Email Uniqueness Check
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            throw new InvalidOperationException($"A user with email '{request.Email}' already exists.");
        }

        var fullName = $"{request.FirstName.Trim()} {request.LastName.Trim()}".Trim();
        var password = string.IsNullOrWhiteSpace(request.Password) ? GenerateSecurePassword() : request.Password;

        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FullName = fullName,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim(),
            Status = "ACTIVE",
            IsActive = true,
            IsDeleted = false,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        // 2. Assign Reporter role strictly
        await _userManager.AddToRoleAsync(user, "Reporter");

        return new ReporterDto
        {
            Id = user.Id,
            Name = user.FullName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Phone = user.PhoneNumber ?? string.Empty,
            Department = user.Department ?? string.Empty,
            Role = "REPORTER",
            Status = "ACTIVE",
            IsActive = true,
            IsDeleted = false,
            SubmittedTicketsCount = 0,
            OpenTicketsCount = 0,
            LastActiveAt = user.CreatedAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public async Task<ReporterDto> UpdateReporterAsync(Guid id, UpdateReporterDto request)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.IsDeleted)
        {
            throw new KeyNotFoundException($"Reporter with ID '{id}' was not found.");
        }

        if (!await _userManager.IsInRoleAsync(user, "Reporter"))
        {
            throw new InvalidOperationException("Only accounts with the Reporter role can be updated through this endpoint.");
        }

        // Email uniqueness if changed
        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _userManager.FindByEmailAsync(request.Email);
            if (existing != null && existing.Id != user.Id)
            {
                throw new InvalidOperationException($"Email '{request.Email}' is already in use by another account.");
            }

            user.Email = request.Email.Trim();
            user.UserName = request.Email.Trim();
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.FullName = $"{user.FirstName} {user.LastName}".Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        user.Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        var tickets = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.ReporterId == id)
            .Select(t => t.Status)
            .ToListAsync();

        var openStatuses = new[] { TicketStatus.New, TicketStatus.Assigned, TicketStatus.InProgress };

        return new ReporterDto
        {
            Id = user.Id,
            Name = user.FullName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email!,
            Phone = user.PhoneNumber ?? string.Empty,
            Department = user.Department ?? string.Empty,
            Role = "REPORTER",
            Status = NormalizeStatus(user.Status, user.IsActive),
            IsActive = user.IsActive,
            IsDeleted = user.IsDeleted,
            DeletedAt = user.DeletedAt,
            SubmittedTicketsCount = tickets.Count,
            OpenTicketsCount = tickets.Count(s => openStatuses.Contains(s)),
            LastActiveAt = user.UpdatedAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public async Task DeleteReporterAsync(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.IsDeleted)
        {
            throw new KeyNotFoundException($"Reporter with ID '{id}' was not found.");
        }

        if (!await _userManager.IsInRoleAsync(user, "Reporter"))
        {
            throw new InvalidOperationException("Only accounts with the Reporter role can be deleted through this endpoint.");
        }

        // Soft deletion:
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.IsActive = false;
        user.Status = "DEACTIVATED";
        user.UpdatedAt = DateTime.UtcNow;

        // Invalidate all active refresh tokens for this user
        var userTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == user.Id && !rt.IsRevoked)
            .ToListAsync();

        foreach (var token in userTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            token.ReasonRevoked = "Account soft deleted by administrator.";
        }

        await _userManager.UpdateAsync(user);
        await _context.SaveChangesAsync();
    }

    public async Task<ReporterDto> UpdateStatusAsync(Guid id, string status)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.IsDeleted)
        {
            throw new KeyNotFoundException($"Reporter with ID '{id}' was not found.");
        }

        if (!await _userManager.IsInRoleAsync(user, "Reporter"))
        {
            throw new InvalidOperationException("Only accounts with the Reporter role can be updated through this endpoint.");
        }

        var normalized = status.Trim().ToUpperInvariant();
        if (normalized != "ACTIVE" && normalized != "SUSPENDED" && normalized != "DEACTIVATED")
        {
            throw new ArgumentException("Status must be Active, Suspended, or Deactivated.");
        }

        user.Status = normalized;
        user.IsActive = normalized == "ACTIVE";
        user.UpdatedAt = DateTime.UtcNow;

        // If suspending or deactivating, revoke active sessions
        if (!user.IsActive)
        {
            var userTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == user.Id && !rt.IsRevoked)
                .ToListAsync();

            foreach (var token in userTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                token.ReasonRevoked = $"Account {normalized.ToLowerInvariant()} by administrator.";
            }
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        await _context.SaveChangesAsync();

        var (fn, ln) = ResolveNames(user);
        var tickets = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.ReporterId == id)
            .Select(t => t.Status)
            .ToListAsync();

        var openStatuses = new[] { TicketStatus.New, TicketStatus.Assigned, TicketStatus.InProgress };

        return new ReporterDto
        {
            Id = user.Id,
            Name = user.FullName,
            FirstName = fn,
            LastName = ln,
            Email = user.Email ?? string.Empty,
            Phone = user.PhoneNumber ?? string.Empty,
            Department = user.Department ?? string.Empty,
            Role = "REPORTER",
            Status = user.Status,
            IsActive = user.IsActive,
            IsDeleted = user.IsDeleted,
            DeletedAt = user.DeletedAt,
            SubmittedTicketsCount = tickets.Count,
            OpenTicketsCount = tickets.Count(s => openStatuses.Contains(s)),
            LastActiveAt = user.UpdatedAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    private static (string FirstName, string LastName) ResolveNames(ApplicationUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.FirstName) || !string.IsNullOrWhiteSpace(user.LastName))
        {
            return (user.FirstName ?? string.Empty, user.LastName ?? string.Empty);
        }

        var parts = (user.FullName ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return (parts.Length > 0 ? parts[0] : string.Empty, parts.Length > 1 ? parts[1] : string.Empty);
    }

    private static string NormalizeStatus(string? status, bool isActive)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            var upper = status.Trim().ToUpperInvariant();
            if (upper == "ACTIVE" || upper == "SUSPENDED" || upper == "DEACTIVATED")
            {
                return upper;
            }
        }

        return isActive ? "ACTIVE" : "DEACTIVATED";
    }

    private static string GenerateSecurePassword()
    {
        // Identity rules: min 6 chars, 1 uppercase, 1 digit, 1 non-alphanumeric
        return "Reporter" + RandomNumberGenerator.GetInt32(100, 999) + "!";
    }
}
