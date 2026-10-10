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

        // 3. Tickets & Assignments
        if (!await context.Tickets.AnyAsync())
        {
            var ticket1 = new Ticket
            {
                Category = "Plumbing",
                Building = "Engineering Building",
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
                Building = "Library",
                Room = "2nd Floor West",
                Description = "Lights flickering.",
                Status = TicketStatus.New,
                ReporterId = reporter2.Id,
                CreatedAt = DateTime.UtcNow.AddHours(-5),
                UpdatedAt = DateTime.UtcNow.AddHours(-5)
            };
            
            var ticket3 = new Ticket
            {
                Category = "IT",
                Building = "Science Hall",
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
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await userManager.CreateAsync(user, password);
            await userManager.AddToRoleAsync(user, role);
        }
        else if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }
        return user;
    }
}

