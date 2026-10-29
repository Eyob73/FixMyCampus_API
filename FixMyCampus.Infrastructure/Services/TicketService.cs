using FixMyCampus.Application.Common;
using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Application.DTOs.Users;
using FixMyCampus.Application.Exceptions;
using FixMyCampus.Application.Interfaces;
using FixMyCampus.Domain.Entities;
using FixMyCampus.Domain.Enums;
using FixMyCampus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace FixMyCampus.Infrastructure.Services;

public class TicketService : ITicketService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly IDuplicateDetectionService _duplicateDetectionService;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FallbackLocks = new();

    public TicketService(
        ApplicationDbContext context,
        IFileStorageService fileStorageService,
        IDuplicateDetectionService duplicateDetectionService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _duplicateDetectionService = duplicateDetectionService;
    }

    public async Task<DuplicateCheckResponse> CheckDuplicateTicketAsync(DuplicateCheckRequest request)
    {
        return await _duplicateDetectionService.CheckDuplicatesAsync(request);
    }

    public async Task<TicketDto> CreateTicketAsync(Guid reporterId, CreateTicketRequest request, IEnumerable<IFormFile>? files = null)
    {
        var reporter = await _context.Users.FindAsync(reporterId) 
            ?? throw new Exception("Reporter not found");

        var fileList = files?.Where(f => f != null && f.Length > 0).ToList() ?? new List<IFormFile>();
        if (fileList.Count > FileValidator.MaxImageCount)
        {
            throw new ArgumentException($"Cannot attach more than {FileValidator.MaxImageCount} images to an issue.");
        }

        // Validate each file before doing any storage or DB changes
        foreach (var file in fileList)
        {
            FileValidator.ValidateImageFile(file);
        }

        var savedFilePaths = new List<(string storagePath, IFormFile file)>();
        var lockKeyString = $"{request.Category?.Trim().ToLowerInvariant()}:{request.Building?.Trim().ToLowerInvariant()}:{DuplicateDetectionService.NormalizeRoom(request.Room)}";
        SemaphoreSlim? fallbackSemaphore = null;

        var isNpgsql = _context.Database.IsNpgsql();
        if (!isNpgsql)
        {
            fallbackSemaphore = FallbackLocks.GetOrAdd(lockKeyString, _ => new SemaphoreSlim(1, 1));
            await fallbackSemaphore.WaitAsync();
        }

        var isRelational = _context.Database.IsRelational();
        var transaction = (isRelational && _context.Database.CurrentTransaction == null) 
            ? await _context.Database.BeginTransactionAsync() 
            : null;

        try
        {
            if (isNpgsql)
            {
                var pgLockKey = ComputeAdvisoryLockKey(request.Category ?? string.Empty, request.Building ?? string.Empty, request.Room);
                await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0});", pgLockKey);
            }

            // 1. Perform duplicate detection within the locked transaction
            var dupCheck = await _duplicateDetectionService.CheckDuplicatesAsync(new DuplicateCheckRequest
            {
                Category = request.Category,
                Building = request.Building,
                Floor = request.Floor,
                Room = request.Room,
                Title = request.Title,
                Description = request.Description
            });

            if (dupCheck.IsDuplicate)
            {
                throw new DuplicateTicketException(
                    dupCheck.Message ?? "A matching active issue has already been reported.",
                    dupCheck.PrimaryMatch!,
                    dupCheck.Matches,
                    isConfirmed: true);
            }

            if (dupCheck.HasPotentialDuplicates && !request.IgnorePotentialDuplicates)
            {
                throw new DuplicateTicketException(
                    dupCheck.Message ?? "A similar active issue was found. Please review existing issues before submitting.",
                    dupCheck.PrimaryMatch!,
                    dupCheck.Matches,
                    isConfirmed: false);
            }

            // 2. Save attachments after duplicate check passes
            foreach (var file in fileList)
            {
                var storagePath = await _fileStorageService.SaveFileAsync(file, "images");
                savedFilePaths.Add((storagePath, file));
            }

            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                ReporterId = reporterId,
                Category = request.Category,
                Building = request.Building,
                Floor = request.Floor,
                Room = request.Room,
                Description = request.Description,
                Title = request.Title,
                Priority = request.Priority,
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

            foreach (var (storagePath, file) in savedFilePaths)
            {
                var ticketImage = new TicketImage
                {
                    Id = Guid.NewGuid(),
                    TicketId = ticket.Id,
                    Category = ImageCategory.ReportedIssue,
                    StoragePath = storagePath,
                    OriginalFileName = Path.GetFileName(file.FileName),
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    UploaderId = reporterId,
                    UploadedAt = DateTime.UtcNow
                };
                _context.TicketImages.Add(ticketImage);
            }

            await _context.SaveChangesAsync();

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            return await MapToDtoAsync(ticket);
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }

            // Avoid orphaned files by deleting anything that was saved to disk
            foreach (var (storagePath, _) in savedFilePaths)
            {
                await _fileStorageService.DeleteFileAsync(storagePath);
            }
            throw;
        }
        finally
        {
            if (fallbackSemaphore != null)
            {
                fallbackSemaphore.Release();
            }
        }
    }

    private static long ComputeAdvisoryLockKey(string category, string building, string? room)
    {
        var raw = $"{category.Trim().ToLowerInvariant()}:{building.Trim().ToLowerInvariant()}:{DuplicateDetectionService.NormalizeRoom(room)}";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return BitConverter.ToInt64(hash, 0);
    }

    public async Task<IReadOnlyList<TicketDto>> GetTicketsAsync(TicketFilterRequest? filter)
    {
        var query = _context.Tickets
            .Include(t => t.Reporter)
            .Include(t => t.Images)
            .Include(t => t.Assignments)
                .ThenInclude(a => a.Technician)
            .AsNoTracking()
            .AsQueryable();

        if (filter != null)
        {
            if (filter.Status.HasValue) query = query.Where(t => t.Status == filter.Status.Value);
            if (!string.IsNullOrEmpty(filter.Category)) query = query.Where(t => t.Category.Contains(filter.Category));
            if (!string.IsNullOrEmpty(filter.Building)) query = query.Where(t => t.Building.Contains(filter.Building));
            if (!string.IsNullOrEmpty(filter.Floor)) query = query.Where(t => t.Floor != null && t.Floor.Contains(filter.Floor));
        }

        var tickets = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
        return tickets.Select(MapToDtoSync).ToList();
    }

    public async Task<IReadOnlyList<TicketDto>> GetMyTicketsAsync(Guid reporterId)
    {
        var query = _context.Tickets
            .Include(t => t.Reporter)
            .Include(t => t.Images)
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
            .Include(t => t.Comments)
                .ThenInclude(c => c.Author)
            .Include(t => t.Images)
                .ThenInclude(i => i.Uploader)
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
            Title = dto.Title,
            Category = dto.Category,
            Building = dto.Building,
            Floor = dto.Floor,
            Room = dto.Room,
            Description = dto.Description,
            Priority = dto.Priority,
            Status = dto.Status,
            Reporter = dto.Reporter,
            AssignedTechnician = dto.AssignedTechnician,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            InternalNotes = ticket.InternalNotes ?? new List<string>(),
            Comments = ticket.Comments?.Select(c => new TicketCommentDto
            {
                Id = c.Id,
                TicketId = c.TicketId,
                AuthorId = c.AuthorId,
                AuthorName = c.Author?.FullName ?? string.Empty,
                AuthorRole = "",
                Content = c.Content,
                CreatedAt = c.CreatedAt
            }).OrderBy(c => c.CreatedAt).ToList() ?? new List<TicketCommentDto>(),
            Images = ticket.Images?.Select(i => new TicketImageDto
            {
                Id = i.Id,
                TicketId = i.TicketId,
                Category = i.Category.ToString(),
                Url = $"/api/Tickets/{i.TicketId}/images/{i.Id}/file",
                FileName = i.OriginalFileName,
                ContentType = i.ContentType,
                FileSize = i.FileSize,
                UploaderId = i.UploaderId,
                UploaderName = i.Uploader?.FullName ?? string.Empty,
                UploadedAt = i.UploadedAt
            }).OrderBy(i => i.UploadedAt).ToList() ?? new List<TicketImageDto>()
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
            Note = h.Note,
            ChangedBy = new UserDto
            {
                Id = h.ChangedBy.Id,
                FullName = h.ChangedBy.FullName,
                Email = h.ChangedBy.Email!
            }
        }).ToList();
    }

    public async Task<TicketDetailsDto> AddInternalNoteAsync(Guid ticketId, Guid currentUserId, bool isAdmin, AddNoteRequest request)
    {
        if (!isAdmin) throw new UnauthorizedAccessException("Only admins can add internal notes.");

        var ticket = await _context.Tickets.FindAsync(ticketId);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found");

        if (ticket.InternalNotes == null) ticket.InternalNotes = new List<string>();
        ticket.InternalNotes.Add($"{DateTime.UtcNow:g}: {request.Note}");
        ticket.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await GetTicketByIdAsync(ticketId, currentUserId, isAdmin);
    }

    public async Task<TicketCommentDto> AddCommentAsync(Guid ticketId, Guid currentUserId, bool isAdmin, AddCommentRequest request)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Assignments)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket == null) throw new KeyNotFoundException("Ticket not found");

        if (!isAdmin && ticket.ReporterId != currentUserId)
        {
            var isTechnician = ticket.Assignments.Any(a => a.TechnicianId == currentUserId && a.UnassignedAt == null);
            if (!isTechnician) throw new UnauthorizedAccessException("Not authorized to comment on this ticket");
        }

        var comment = new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorId = currentUserId,
            Content = request.Content,
            CreatedAt = DateTime.UtcNow
        };

        _context.TicketComments.Add(comment);
        ticket.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var author = await _context.Users.FindAsync(currentUserId);
        
        return new TicketCommentDto
        {
            Id = comment.Id,
            TicketId = comment.TicketId,
            AuthorId = comment.AuthorId,
            AuthorName = author?.FullName ?? string.Empty,
            AuthorRole = "",
            Content = comment.Content,
            CreatedAt = comment.CreatedAt
        };
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
            Title = ticket.Title,
            Category = ticket.Category,
            Building = ticket.Building,
            Floor = ticket.Floor,
            Room = ticket.Room,
            Description = ticket.Description,
            Priority = ticket.Priority.ToString(),
            Status = ticket.Status.ToString(),
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
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
            } : null,
            Images = ticket.Images?.Select(i => new TicketImageDto
            {
                Id = i.Id,
                TicketId = i.TicketId,
                Category = i.Category.ToString(),
                Url = $"/api/Tickets/{i.TicketId}/images/{i.Id}/file",
                FileName = i.OriginalFileName,
                ContentType = i.ContentType,
                FileSize = i.FileSize,
                UploaderId = i.UploaderId,
                UploaderName = string.Empty,
                UploadedAt = i.UploadedAt
            }).OrderBy(i => i.UploadedAt).ToList() ?? new List<TicketImageDto>()
        };
    }

    public async Task<FixMyCampus.Application.DTOs.Dashboard.ReporterDashboardDto> GetReporterDashboardAsync(Guid reporterId)
    {
        var tickets = await _context.Tickets
            .Where(t => t.ReporterId == reporterId)
            .AsNoTracking()
            .ToListAsync();

        return new FixMyCampus.Application.DTOs.Dashboard.ReporterDashboardDto
        {
            TotalTickets = tickets.Count,
            NewTickets = tickets.Count(t => t.Status == TicketStatus.New),
            AssignedTickets = tickets.Count(t => t.Status == TicketStatus.Assigned),
            InProgressTickets = tickets.Count(t => t.Status == TicketStatus.InProgress),
            ResolvedTickets = tickets.Count(t => t.Status == TicketStatus.Resolved)
        };
    }


    public async Task<FixMyCampus.Application.DTOs.Dashboard.AdminDashboardDto> GetAdminDashboardAsync()
    {
        var tickets = await _context.Tickets
            .Include(t => t.Reporter)
            .Include(t => t.Assignments)
                .ThenInclude(a => a.Technician)
            .AsNoTracking()
            .ToListAsync();

        var recentTickets = tickets
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .Select(MapToDtoSync)
            .ToList();

        return new FixMyCampus.Application.DTOs.Dashboard.AdminDashboardDto
        {
            TotalTickets = tickets.Count,
            NewTickets = tickets.Count(t => t.Status == TicketStatus.New),
            AssignedTickets = tickets.Count(t => t.Status == TicketStatus.Assigned),
            InProgressTickets = tickets.Count(t => t.Status == TicketStatus.InProgress),
            ResolvedTickets = tickets.Count(t => t.Status == TicketStatus.Resolved),
            RecentTickets = recentTickets
        };
    }

    public async Task<TicketDetailsDto> UpdatePriorityAsync(Guid ticketId, Guid currentUserId, bool isAdmin, TicketPriority newPriority)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Assignments)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket == null) throw new KeyNotFoundException("Ticket not found");

        if (!isAdmin)
        {
            var isTechnician = ticket.Assignments.Any(a => a.TechnicianId == currentUserId && a.UnassignedAt == null);
            var isReporter = ticket.ReporterId == currentUserId;
            if (!isTechnician && !isReporter) throw new UnauthorizedAccessException("Not authorized to update priority on this ticket");
        }

        if (ticket.Priority != newPriority)
        {
            ticket.Priority = newPriority;
            ticket.UpdatedAt = DateTime.UtcNow;

            var history = new TicketHistory
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                OldStatus = ticket.Status,
                NewStatus = ticket.Status, // Status unchanged, priority changed
                Note = $"Priority updated to {newPriority}",
                ChangedById = currentUserId,
                ChangedAt = DateTime.UtcNow
            };
            
            _context.TicketHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        return await GetTicketByIdAsync(ticketId, currentUserId, isAdmin);
    }

    public async Task<TicketDetailsDto> UpdateStatusAsync(Guid ticketId, Guid currentUserId, bool isAdmin, TicketStatus newStatus, string? note)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Assignments)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket == null) throw new KeyNotFoundException("Ticket not found");

        if (!isAdmin)
        {
            var isTechnician = ticket.Assignments.Any(a => a.TechnicianId == currentUserId && a.UnassignedAt == null);
            if (!isTechnician) throw new UnauthorizedAccessException("Not authorized to update status on this ticket");
        }

        if (ticket.Status != newStatus)
        {
            var oldStatus = ticket.Status;
            ticket.Status = newStatus;
            ticket.UpdatedAt = DateTime.UtcNow;

            var history = new TicketHistory
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                Note = !string.IsNullOrWhiteSpace(note) ? note : $"Status transitioned to {newStatus}",
                ChangedById = currentUserId,
                ChangedAt = DateTime.UtcNow
            };

            _context.TicketHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        return await GetTicketByIdAsync(ticketId, currentUserId, isAdmin);
    }

    public async Task<List<TicketImageDto>> AddTicketImagesAsync(
        Guid ticketId,
        Guid currentUserId,
        bool isAdmin,
        ImageCategory category,
        IEnumerable<IFormFile> files)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Assignments)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket == null) throw new KeyNotFoundException("Ticket not found");

        if (!isAdmin)
        {
            if (category == ImageCategory.ReportedIssue)
            {
                if (ticket.ReporterId != currentUserId)
                    throw new UnauthorizedAccessException("Only the reporter or administrator can add reported issue images.");
            }
            else if (category == ImageCategory.ResolutionEvidence)
            {
                var isAssigned = ticket.Assignments.Any(a => a.TechnicianId == currentUserId && a.UnassignedAt == null);
                if (!isAssigned)
                    throw new UnauthorizedAccessException("Only the assigned technician or administrator can upload resolution evidence.");
            }
        }

        var fileList = files?.Where(f => f != null && f.Length > 0).ToList() ?? new List<IFormFile>();
        if (fileList.Count == 0)
        {
            throw new ArgumentException("No valid image files provided for upload.");
        }

        if (fileList.Count > FileValidator.MaxImageCount)
        {
            throw new ArgumentException($"Cannot upload more than {FileValidator.MaxImageCount} images at once.");
        }

        foreach (var file in fileList)
        {
            FileValidator.ValidateImageFile(file);
        }

        var uploader = await _context.Users.FindAsync(currentUserId);
        var savedFilePaths = new List<(string storagePath, IFormFile file)>();
        var addedImages = new List<TicketImage>();

        try
        {
            foreach (var file in fileList)
            {
                var storagePath = await _fileStorageService.SaveFileAsync(file, "images");
                savedFilePaths.Add((storagePath, file));
            }

            foreach (var (storagePath, file) in savedFilePaths)
            {
                var img = new TicketImage
                {
                    Id = Guid.NewGuid(),
                    TicketId = ticket.Id,
                    Category = category,
                    StoragePath = storagePath,
                    OriginalFileName = Path.GetFileName(file.FileName),
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    UploaderId = currentUserId,
                    UploadedAt = DateTime.UtcNow
                };
                _context.TicketImages.Add(img);
                addedImages.Add(img);
            }

            ticket.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return addedImages.Select(i => new TicketImageDto
            {
                Id = i.Id,
                TicketId = i.TicketId,
                Category = i.Category.ToString(),
                Url = $"/api/Tickets/{i.TicketId}/images/{i.Id}/file",
                FileName = i.OriginalFileName,
                ContentType = i.ContentType,
                FileSize = i.FileSize,
                UploaderId = i.UploaderId,
                UploaderName = uploader?.FullName ?? string.Empty,
                UploadedAt = i.UploadedAt
            }).ToList();
        }
        catch
        {
            foreach (var (storagePath, _) in savedFilePaths)
            {
                await _fileStorageService.DeleteFileAsync(storagePath);
            }
            throw;
        }
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> GetTicketImageStreamAsync(
        Guid ticketId,
        Guid imageId)
    {
        var image = await _context.TicketImages
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == imageId && i.TicketId == ticketId);

        if (image == null) throw new KeyNotFoundException("Image not found");

        return await _fileStorageService.GetFileAsync(image.StoragePath, image.OriginalFileName, image.ContentType);
    }
}
