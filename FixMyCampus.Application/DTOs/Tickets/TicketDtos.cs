using FixMyCampus.Domain.Enums;
using FixMyCampus.Application.DTOs.Users;

namespace FixMyCampus.Application.DTOs.Tickets;

public class CreateTicketRequest
{
    public string Category { get; set; } = string.Empty;
    public string Building { get; set; } = string.Empty;
    public string? Room { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class TicketFilterRequest
{
    public TicketStatus? Status { get; set; }
    public string? Category { get; set; }
    public string? Building { get; set; }
}

public class TicketDto
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Building { get; set; } = string.Empty;
    public string? Room { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public UserDto Reporter { get; set; } = null!;
    public UserDto? AssignedTechnician { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TicketDetailsDto : TicketDto
{
    public DateTime UpdatedAt { get; set; }
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
}

public class AssignTechnicianRequest
{
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
    public string Note { get; set; } = string.Empty;
}

public class AddCommentRequest
{
    public string Content { get; set; } = string.Empty;
}
