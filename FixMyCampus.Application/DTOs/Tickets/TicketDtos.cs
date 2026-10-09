using FixMyCampus.Domain.Enums;
using FixMyCampus.Application.DTOs.Users;
using System.ComponentModel.DataAnnotations;

namespace FixMyCampus.Application.DTOs.Tickets;

public class CreateTicketRequest
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required")]
    [StringLength(50, ErrorMessage = "Category cannot exceed 50 characters")]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "Building is required")]
    [StringLength(50, ErrorMessage = "Building cannot exceed 50 characters")]
    public string Building { get; set; } = string.Empty;

    public string? Room { get; set; }

    [Required(ErrorMessage = "Description is required")]
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string Description { get; set; } = string.Empty;

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
}

public class TicketFilterRequest
{
    public TicketStatus? Status { get; set; }
    public string? Category { get; set; }
    public string? Building { get; set; }
    public TicketPriority? Priority { get; set; }
}

public class TicketDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Building { get; set; } = string.Empty;
    public string? Room { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public UserDto Reporter { get; set; } = null!;
    public UserDto? AssignedTechnician { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TicketDetailsDto : TicketDto
{
    public List<string> InternalNotes { get; set; } = new List<string>();
    public List<TicketCommentDto> Comments { get; set; } = new List<TicketCommentDto>();
}

public class TicketHistoryDto
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string? OldStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public UserDto ChangedBy { get; set; } = null!;
    public DateTime ChangedAt { get; set; }
    public string? Note { get; set; }
}

public class AssignTechnicianRequest
{
    [Required(ErrorMessage = "TechnicianId is required")]
    public Guid TechnicianId { get; set; }
}

public class TicketCommentDto
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorRole { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AddNoteRequest
{
    [Required(ErrorMessage = "Note content is required")]
    public string Note { get; set; } = string.Empty;
}

public class AddCommentRequest
{
    [Required(ErrorMessage = "Comment content is required")]
    public string Content { get; set; } = string.Empty;
}

public class ResolveTicketRequest
{
    public string? ResolutionNote { get; set; }
}
