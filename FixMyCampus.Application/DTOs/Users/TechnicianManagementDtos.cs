using System.ComponentModel.DataAnnotations;

namespace FixMyCampus.Application.DTOs.Users;

public class UpdateTechnicianDto
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(150, ErrorMessage = "Name cannot exceed 150 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone is required")]
    [Phone(ErrorMessage = "Invalid phone number format")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Department is required")]
    [StringLength(100, ErrorMessage = "Department cannot exceed 100 characters")]
    public string Department { get; set; } = string.Empty;

    [Required(ErrorMessage = "Specialty is required")]
    [StringLength(100, ErrorMessage = "Specialty cannot exceed 100 characters")]
    public string Specialty { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = "AVAILABLE";
}

public class UpdateTechnicianStatusDto
{
    [Required(ErrorMessage = "Status is required")]
    public string Status { get; set; } = "AVAILABLE";
}

public class TechnicianQueryParameters
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Department { get; set; }
    public string? SortBy { get; set; } = "createdAt"; // name, email, createdAt, status, tickets, department
    public bool SortDescending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class TechnicianStatsDto
{
    public int TotalTechnicians { get; set; }
    public int AvailableTechnicians { get; set; }
    public int BusyTechnicians { get; set; }
    public int OnCallTechnicians { get; set; }
    public int OffDutyTechnicians { get; set; }
}

public class TechnicianDetailDto : TechnicianDto
{
    public List<ReporterTicketSummaryDto> AssignedTickets { get; set; } = new();
}

