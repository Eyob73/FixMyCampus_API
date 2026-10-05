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
