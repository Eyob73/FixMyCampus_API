namespace FixMyCampus.Application.DTOs.Tickets;

public class DuplicateCheckRequest
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Building { get; set; } = string.Empty;
    public string? Floor { get; set; }
    public string? Room { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class DuplicateTicketMatchDto
{
    public Guid TicketId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Building { get; set; } = string.Empty;
    public string? Floor { get; set; }
    public string? Room { get; set; }
    public DateTime CreatedAt { get; set; }
    public double SimilarityScore { get; set; }
    public string MatchLevel { get; set; } = string.Empty; // "Confirmed" | "Potential"
    public string Reason { get; set; } = string.Empty;
}

public class DuplicateCheckResponse
{
    public bool IsDuplicate { get; set; }
    public bool HasPotentialDuplicates { get; set; }
    public string? Message { get; set; }
    public DuplicateTicketMatchDto? PrimaryMatch { get; set; }
    public List<DuplicateTicketMatchDto> Matches { get; set; } = new();
}

public class DuplicateConflictResponse
{
    public string Message { get; set; } = string.Empty;
    public DuplicateTicketMatchDto? DuplicateTicket { get; set; }
    public List<DuplicateTicketMatchDto> Matches { get; set; } = new();
}
