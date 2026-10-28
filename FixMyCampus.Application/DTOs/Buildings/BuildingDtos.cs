using System.ComponentModel.DataAnnotations;

namespace FixMyCampus.Application.DTOs.Buildings;

public class CreateBuildingDto
{
    [Required(ErrorMessage = "Code is required")]
    [StringLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Name is required")]
    [StringLength(150, ErrorMessage = "Name cannot exceed 150 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Zone is required")]
    [StringLength(100, ErrorMessage = "Zone cannot exceed 100 characters")]
    public string Zone { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "Floors must be at least 1")]
    public int Floors { get; set; } = 1;

    [Range(1, 10000, ErrorMessage = "Total rooms must be at least 1")]
    public int TotalRooms { get; set; } = 1;

    [StringLength(100, ErrorMessage = "Manager name cannot exceed 100 characters")]
    public string? ManagerName { get; set; }

    [StringLength(150, ErrorMessage = "Manager contact cannot exceed 150 characters")]
    public string? ManagerContact { get; set; }

    public string Status { get; set; } = "OPERATIONAL";
}

public class UpdateBuildingDto
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Zone { get; set; }
    public int? Floors { get; set; }
    public int? TotalRooms { get; set; }
    public string? ManagerName { get; set; }
    public string? ManagerContact { get; set; }
    public string? Status { get; set; }
}

public class BuildingDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public int Floors { get; set; }
    public int TotalRooms { get; set; }
    public int ActiveTicketsCount { get; set; }
    public string? ManagerName { get; set; }
    public string? ManagerContact { get; set; }
    public string Status { get; set; } = "OPERATIONAL";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
