namespace FixMyCampus.Application.DTOs.Users;

public class TechnicianDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string Status { get; set; } = "AVAILABLE";
    public int AssignedTicketCount { get; set; }
    public int ActiveTicketsCount { get; set; }
    public int ResolvedTicketsCount { get; set; }
    public double Rating { get; set; }
    public DateTime CreatedAt { get; set; }
}
