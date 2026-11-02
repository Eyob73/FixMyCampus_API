using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Domain.Entities;
using FixMyCampus.Domain.Enums;
using FixMyCampus.Infrastructure.Persistence;
using FixMyCampus.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FixMyCampus.Tests;

public class TicketSoftDeleteTests
{
    private (ApplicationDbContext Context, TicketService Service, AdminService AdminService, Guid ReporterId, Guid AdminId) CreateTestContext()
    {
        var dbName = $"SoftDeleteTestDb_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new ApplicationDbContext(options);

        var reporterId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var reporter = new ApplicationUser
        {
            Id = reporterId,
            UserName = "user@test.local",
            Email = "user@test.local",
            FullName = "Reporter One"
        };

        var admin = new ApplicationUser
        {
            Id = adminId,
            UserName = "admin@test.local",
            Email = "admin@test.local",
            FullName = "Admin User"
        };

        context.Users.AddRange(reporter, admin);
        context.SaveChanges();

        var storage = new TestFileStorageService();
        var duplicateService = new DuplicateDetectionService(context);
        var notificationService = new TestNotificationService();
        var ticketService = new TicketService(context, storage, duplicateService, notificationService);
        var adminService = new AdminService(context, null!, ticketService, notificationService);

        return (context, ticketService, adminService, reporterId, adminId);
    }

    [Fact]
    public async Task DeleteTicketAsync_SetsIsDeletedAndPreservesData()
    {
        var (context, service, _, reporterId, adminId) = CreateTestContext();

        // Arrange
        var created = await service.CreateTicketAsync(reporterId, new CreateTicketRequest
        {
            Title = "Broken AC in Hall",
            Category = "HVAC",
            Building = "Engineering",
            Floor = "2",
            Room = "201",
            Description = "Air conditioner is leaking water",
            Priority = TicketPriority.High
        });

        // Add a comment to the ticket
        await service.AddCommentAsync(created.Id, reporterId, false, new AddCommentRequest
        {
            Content = "Please fix as soon as possible"
        });

        // Act - Soft Delete
        await service.DeleteTicketAsync(created.Id, adminId);

        // Assert directly against DB ignoring query filters
        var rawTicket = await context.Tickets
            .IgnoreQueryFilters()
            .Include(t => t.Comments)
            .Include(t => t.Histories)
            .FirstOrDefaultAsync(t => t.Id == created.Id);

        Assert.NotNull(rawTicket);
        Assert.True(rawTicket.IsDeleted);
        Assert.NotNull(rawTicket.DeletedAt);
        Assert.True(rawTicket.DeletedAt <= DateTime.UtcNow);

        // Verify comments and history preserved
        Assert.Single(rawTicket.Comments);
        Assert.Equal("Please fix as soon as possible", rawTicket.Comments.First().Content);

        // Verify delete history record was created
        Assert.Contains(rawTicket.Histories, h => h.Note != null && h.Note.Contains("soft delete"));
    }

    [Fact]
    public async Task SoftDeletedTicket_IsExcludedFromNormalQueriesAndDashboards()
    {
        var (context, service, adminService, reporterId, adminId) = CreateTestContext();

        // Create 2 tickets
        var ticket1 = await service.CreateTicketAsync(reporterId, new CreateTicketRequest
        {
            Title = "First issue",
            Category = "Electrical",
            Building = "Science",
            Description = "Flickering lights"
        });

        var ticket2 = await service.CreateTicketAsync(reporterId, new CreateTicketRequest
        {
            Title = "Second issue",
            Category = "Plumbing",
            Building = "Science",
            Description = "Leaky tap"
        });

        // Delete ticket1
        await service.DeleteTicketAsync(ticket1.Id, adminId);

        // 1. GetTicketsAsync should only contain ticket2
        var allTickets = await service.GetTicketsAsync(null);
        Assert.Single(allTickets);
        Assert.Equal(ticket2.Id, allTickets.First().Id);

        // 2. GetMyTicketsAsync should only contain ticket2
        var myTickets = await service.GetMyTicketsAsync(reporterId);
        Assert.Single(myTickets);
        Assert.Equal(ticket2.Id, myTickets.First().Id);

        // 3. GetTicketByIdAsync for deleted ticket should throw KeyNotFoundException
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetTicketByIdAsync(ticket1.Id, adminId, true));

        // 4. Admin dashboard stats should only reflect active ticket
        var dashboard = await adminService.GetDashboardAsync();
        Assert.Equal(1, dashboard.TotalTickets);
        Assert.Equal(1, dashboard.NewTickets);
    }

    [Fact]
    public async Task DeleteTicketAsync_ThrowsKeyNotFound_WhenAlreadyDeletedOrNonExistent()
    {
        var (_, service, _, reporterId, adminId) = CreateTestContext();

        var ticket = await service.CreateTicketAsync(reporterId, new CreateTicketRequest
        {
            Title = "Issue",
            Category = "General",
            Building = "Library",
            Description = "Door handle loose"
        });

        await service.DeleteTicketAsync(ticket.Id, adminId);

        // Deleting again should throw KeyNotFoundException
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.DeleteTicketAsync(ticket.Id, adminId));

        // Deleting random ID should throw KeyNotFoundException
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.DeleteTicketAsync(Guid.NewGuid(), adminId));
    }
}
