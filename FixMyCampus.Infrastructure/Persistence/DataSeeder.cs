using FixMyCampus.Domain.Entities;
using FixMyCampus.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FixMyCampus.Infrastructure.Persistence;

public static class DataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole<Guid>> roleManager)
    {
        // 1. Roles
        var roles = new[] { "Admin", "Technician", "Reporter" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        // 2. Users
        var admin = await EnsureUserAsync(userManager, "admin@hackathon.local", "Admin", "Admin123!", "Admin");
        var tech1 = await EnsureUserAsync(userManager, "tech@hackathon.local", "Campus Technician 1", "Tech123!", "Technician");
        var tech2 = await EnsureUserAsync(userManager, "tech2@hackathon.local", "Campus Technician 2", "Tech123!", "Technician");
        var reporter1 = await EnsureUserAsync(userManager, "user@hackathon.local", "Standard User", "User123!", "Reporter");
        var reporter2 = await EnsureUserAsync(userManager, "student2@hackathon.local", "Jane Smith", "User123!", "Reporter");

        // 3. Categories
        if (!await context.Categories.AnyAsync())
        {
            var categories = new List<Category>
            {
                new()
                {
                    Name = "Plumbing",
                    Description = "Pipes, drains, leaks, restrooms, sinks, and water fixtures",
                    Icon = "plumbing",
                    DisplayOrder = 1,
                    IsActive = true
                },
                new()
                {
                    Name = "Electrical",
                    Description = "Power outlets, breakers, lighting, switches, and wiring",
                    Icon = "bolt",
                    DisplayOrder = 2,
                    IsActive = true
                },
                new()
                {
                    Name = "HVAC / Climate",
                    Description = "Heating, air conditioning, ventilation, and thermostat issues",
                    Icon = "mode_fan",
                    DisplayOrder = 3,
                    IsActive = true
                },
                new()
                {
                    Name = "Equipment",
                    Description = "Lab equipment, AV projectors, machinery, and appliances",
                    Icon = "devices",
                    DisplayOrder = 4,
                    IsActive = true
                },
                new()
                {
                    Name = "Furniture",
                    Description = "Desks, chairs, whiteboards, podiums, and shelving",
                    Icon = "chair",
                    DisplayOrder = 5,
                    IsActive = true
                },
                new()
                {
                    Name = "Structural & Doors",
                    Description = "Doors, locks, windows, walls, ceiling tiles, and stairs",
                    Icon = "door_front",
                    DisplayOrder = 6,
                    IsActive = true
                },
                new()
                {
                    Name = "Network & Wi-Fi",
                    Description = "Campus Wi-Fi connectivity, Ethernet ports, and network hardware",
                    Icon = "wifi",
                    DisplayOrder = 7,
                    IsActive = true
                },
                new()
                {
                    Name = "Cleaning & Grounds",
                    Description = "Spills, sanitation, trash disposal, landscaping, and grounds",
                    Icon = "cleaning_services",
                    DisplayOrder = 8,
                    IsActive = true
                },
                new()
                {
                    Name = "Safety & Locks",
                    Description = "Fire alarms, emergency lighting, access cards, and security hardware",
                    Icon = "security",
                    DisplayOrder = 9,
                    IsActive = true
                },
                new()
                {
                    Name = "Other",
                    Description = "General campus issues not covered by other categories",
                    Icon = "more_horiz",
                    DisplayOrder = 10,
                    IsActive = true
                }
            };

            await context.Categories.AddRangeAsync(categories);
            await context.SaveChangesAsync();
        }

        // 4. Buildings
        if (!await context.Buildings.AnyAsync())
        {
            var buildings = new List<Building>
            {
                new()
                {
                    Code = "SCI-A",
                    Name = "Physical Sciences Hall & Labs",
                    Zone = "STEM_COMPLEX",
                    Floors = 5,
                    TotalRooms = 120,
                    ManagerName = "Robert Vance",
                    ManagerContact = "r.vance@facilities.campus.edu",
                    Status = "OPERATIONAL",
                    CreatedAt = DateTime.UtcNow.AddMonths(-6),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-1)
                },
                new()
                {
                    Code = "ENG-MAIN",
                    Name = "Engineering Research Center",
                    Zone = "STEM_COMPLEX",
                    Floors = 6,
                    TotalRooms = 145,
                    ManagerName = "Kirsten Dale",
                    ManagerContact = "k.dale@facilities.campus.edu",
                    Status = "OPERATIONAL",
                    CreatedAt = DateTime.UtcNow.AddMonths(-6),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-1)
                },
                new()
                {
                    Code = "LIB-CENTRAL",
                    Name = "W.E.B. Du Bois Memorial Library",
                    Zone = "CENTRAL_QUAD",
                    Floors = 8,
                    TotalRooms = 90,
                    ManagerName = "Arthur Dent",
                    ManagerContact = "a.dent@facilities.campus.edu",
                    Status = "OPERATIONAL",
                    CreatedAt = DateTime.UtcNow.AddMonths(-5),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-1)
                },
                new()
                {
                    Code = "RES-NORTH",
                    Name = "North Quad Residential Commons",
                    Zone = "NORTH_CAMPUS",
                    Floors = 4,
                    TotalRooms = 210,
                    ManagerName = "Maria Gallagher",
                    ManagerContact = "m.gallagher@facilities.campus.edu",
                    Status = "MAINTENANCE_SURGE",
                    CreatedAt = DateTime.UtcNow.AddMonths(-5),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-1)
                },
                new()
                {
                    Code = "ATH-PAV",
                    Name = "Campus Pavilion & Aquatic Center",
                    Zone = "WEST_ATHLETICS",
                    Floors = 3,
                    TotalRooms = 45,
                    ManagerName = "Coach Thomas Harris",
                    ManagerContact = "t.harris@athletics.campus.edu",
                    Status = "OPERATIONAL",
                    CreatedAt = DateTime.UtcNow.AddMonths(-4),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-1)
                },
                new()
                {
                    Code = "ADM-TOWER",
                    Name = "Founders Administrative Tower",
                    Zone = "CENTRAL_QUAD",
                    Floors = 10,
                    TotalRooms = 160,
                    ManagerName = "Helen Morales",
                    ManagerContact = "h.morales@facilities.campus.edu",
                    Status = "OPERATIONAL",
                    CreatedAt = DateTime.UtcNow.AddMonths(-6),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-1)
                },
                new()
                {
                    Code = "STU-UNION",
                    Name = "Student Union",
                    Zone = "CENTRAL_QUAD",
                    Floors = 3,
                    TotalRooms = 55,
                    ManagerName = "David Kim",
                    ManagerContact = "d.kim@facilities.campus.edu",
                    Status = "OPERATIONAL",
                    CreatedAt = DateTime.UtcNow.AddMonths(-4),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-1)
                },
                new()
                {
                    Code = "RES-SOUTH",
                    Name = "South Residential Hall",
                    Zone = "SOUTH_CAMPUS",
                    Floors = 5,
                    TotalRooms = 180,
                    ManagerName = "Sarah Jenkins",
                    ManagerContact = "s.jenkins@facilities.campus.edu",
                    Status = "OPERATIONAL",
                    CreatedAt = DateTime.UtcNow.AddMonths(-4),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-1)
                },
                new()
                {
                    Code = "HLT-SCI",
                    Name = "Health Sciences Center",
                    Zone = "STEM_COMPLEX",
                    Floors = 4,
                    TotalRooms = 95,
                    ManagerName = "Dr. Angela Martin",
                    ManagerContact = "a.martin@health.campus.edu",
                    Status = "OPERATIONAL",
                    CreatedAt = DateTime.UtcNow.AddMonths(-3),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-1)
                },
                new()
                {
                    Code = "HUM-HALL",
                    Name = "Humanities Hall",
                    Zone = "CENTRAL_QUAD",
                    Floors = 4,
                    TotalRooms = 75,
                    ManagerName = "Edward Norton",
                    ManagerContact = "e.norton@facilities.campus.edu",
                    Status = "OPERATIONAL",
                    CreatedAt = DateTime.UtcNow.AddMonths(-3),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-1)
                }
            };

            await context.Buildings.AddRangeAsync(buildings);
            await context.SaveChangesAsync();
        }

        // 5. Tickets & Assignments
        if (!await context.Tickets.AnyAsync())
        {
            var ticket1 = new Ticket
            {
                Category = "Plumbing",
                Building = "Engineering Research Center",
                Floor = "1st Floor",
                Room = "101",
                Description = "Leaking pipe under the sink.",
                Status = TicketStatus.Assigned,
                ReporterId = reporter1.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            };

            var ticket2 = new Ticket
            {
                Category = "Electrical",
                Building = "W.E.B. Du Bois Memorial Library",
                Floor = "2nd Floor",
                Room = "West Wing",
                Description = "Lights flickering.",
                Status = TicketStatus.New,
                ReporterId = reporter2.Id,
                CreatedAt = DateTime.UtcNow.AddHours(-5),
                UpdatedAt = DateTime.UtcNow.AddHours(-5)
            };
            
            var ticket3 = new Ticket
            {
                Category = "Equipment",
                Building = "Physical Sciences Hall & Labs",
                Floor = "Ground Floor",
                Room = "Lab 3",
                Description = "Projector not working.",
                Status = TicketStatus.Resolved,
                ReporterId = reporter1.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            };

            context.Tickets.AddRange(ticket1, ticket2, ticket3);
            await context.SaveChangesAsync();

            // Assignment for Ticket 1
            var assignment1 = new TicketAssignment
            {
                TicketId = ticket1.Id,
                TechnicianId = tech1.Id,
                AssignedById = admin.Id,
                AssignedAt = DateTime.UtcNow.AddDays(-1)
            };

            var assignment2 = new TicketAssignment
            {
                TicketId = ticket3.Id,
                TechnicianId = tech2.Id,
                AssignedById = admin.Id,
                AssignedAt = DateTime.UtcNow.AddDays(-4),
                UnassignedAt = DateTime.UtcNow.AddDays(-1) // finished
            };
            
            context.TicketAssignments.AddRange(assignment1, assignment2);
            await context.SaveChangesAsync();
            
            // Ticket History
            var history1 = new TicketHistory
            {
                TicketId = ticket1.Id,
                ChangedById = admin.Id,
                OldStatus = TicketStatus.New,
                NewStatus = TicketStatus.Assigned,
                ChangedAt = DateTime.UtcNow.AddDays(-1)
            };

            var history2 = new TicketHistory
            {
                TicketId = ticket3.Id,
                ChangedById = admin.Id,
                OldStatus = TicketStatus.New,
                NewStatus = TicketStatus.Assigned,
                ChangedAt = DateTime.UtcNow.AddDays(-4)
            };

            var history3 = new TicketHistory
            {
                TicketId = ticket3.Id,
                ChangedById = tech2.Id,
                OldStatus = TicketStatus.Assigned,
                NewStatus = TicketStatus.Resolved,
                ChangedAt = DateTime.UtcNow.AddDays(-1)
            };

            context.TicketHistories.AddRange(history1, history2, history3);
            await context.SaveChangesAsync();
        }
    }

    private static async Task<ApplicationUser> EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string fullName, string password, string role)
    {
        var nameParts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts.Length > 0 ? nameParts[0] : fullName;
        var lastName = nameParts.Length > 1 ? nameParts[1] : "";

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = true,
                Status = "ACTIVE",
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await userManager.CreateAsync(user, password);
            await userManager.AddToRoleAsync(user, role);
        }
        else
        {
            var needsUpdate = false;
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                needsUpdate = true;
            }
            if (string.IsNullOrEmpty(user.FirstName))
            {
                user.FirstName = firstName;
                user.LastName = lastName;
                needsUpdate = true;
            }
            if (string.IsNullOrEmpty(user.Status))
            {
                user.Status = "ACTIVE";
                needsUpdate = true;
            }
            if (needsUpdate)
            {
                await userManager.UpdateAsync(user);
            }
        }
        return user;
    }
}

