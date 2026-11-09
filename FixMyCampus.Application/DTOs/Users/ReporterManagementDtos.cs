using System.ComponentModel.DataAnnotations;

namespace FixMyCampus.Application.DTOs.Users;

public class CreateReporterDto
{
    [Required(ErrorMessage = "First name is required")]
    [StringLength(100, ErrorMessage = "First name cannot exceed 100 characters")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(100, ErrorMessage = "Last name cannot exceed 100 characters")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address format")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number format")]
    [StringLength(25, ErrorMessage = "Phone number cannot exceed 25 characters")]
    public string? Phone { get; set; }

    [StringLength(100, ErrorMessage = "Department cannot exceed 100 characters")]
    public string? Department { get; set; }

    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
    public string? Password { get; set; }
}

public class UpdateReporterDto
{
    [Required(ErrorMessage = "First name is required")]
    [StringLength(100, ErrorMessage = "First name cannot exceed 100 characters")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(100, ErrorMessage = "Last name cannot exceed 100 characters")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address format")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number format")]
    [StringLength(25, ErrorMessage = "Phone number cannot exceed 25 characters")]
    public string? Phone { get; set; }

    [StringLength(100, ErrorMessage = "Department cannot exceed 100 characters")]
    public string? Department { get; set; }
}

public class UpdateReporterStatusDto
{
    [Required(ErrorMessage = "Status is required")]
    [RegularExpression("^(ACTIVE|SUSPENDED|DEACTIVATED|Active|Suspended|Deactivated)$", ErrorMessage = "Status must be Active, Suspended, or Deactivated")]
    public string Status { get; set; } = "ACTIVE";
}

public class ReporterQueryParameters
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? SortBy { get; set; } = "createdAt"; // name, email, createdAt, status, tickets
    public bool SortDescending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class PaginatedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public class ReporterTicketSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Building { get; set; } = string.Empty;
    public string? Floor { get; set; }
    public string? Room { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ReporterDetailDto : ReporterDto
{
    public List<ReporterTicketSummaryDto> Tickets { get; set; } = new();
    public int NewTicketsCount { get; set; }
    public int AssignedTicketsCount { get; set; }
    public int InProgressTicketsCount { get; set; }
    public int ResolvedTicketsCount { get; set; }
    public int ClosedTicketsCount { get; set; }
}

public class ReporterStatsDto
{
    public int TotalReporters { get; set; }
    public int ActiveReporters { get; set; }
    public int SuspendedReporters { get; set; }
    public int DeactivatedReporters { get; set; }
}

