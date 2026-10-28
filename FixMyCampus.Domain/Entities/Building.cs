namespace FixMyCampus.Domain.Entities;

public class Building
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public int Floors { get; set; }
    public int TotalRooms { get; set; }
    public string? ManagerName { get; set; }
    public string? ManagerContact { get; set; }
    public string Status { get; set; } = "OPERATIONAL";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
