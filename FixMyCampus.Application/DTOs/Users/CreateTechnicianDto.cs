namespace FixMyCampus.Application.DTOs.Users;

public class CreateTechnicianDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string Status { get; set; } = "AVAILABLE";
}
