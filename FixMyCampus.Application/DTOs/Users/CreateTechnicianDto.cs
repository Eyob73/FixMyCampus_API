using System.ComponentModel.DataAnnotations;

namespace FixMyCampus.Application.DTOs.Users;

public class CreateTechnicianDto
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
