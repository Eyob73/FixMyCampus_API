using System.Security.Claims;
using FixMyCampus.Api.Controllers;
using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Domain.Entities;
using FixMyCampus.Domain.Enums;
using FixMyCampus.Infrastructure.Persistence;
using FixMyCampus.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FixMyCampus.Tests;

public class TicketInternalNotesTests
{
    private sealed record TestFixture(
        string DbName,
        ApplicationDbContext Context,
        TicketService Service,
        Guid AdminId,
        Guid ReporterId,
        Guid TechnicianId,
        Guid OtherTechnicianId);

    private static TestFixture CreateFixture()
    {
        var dbName = $"InternalNotesTestDb_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new ApplicationDbContext(options);

        var adminId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();
        var technicianId = Guid.NewGuid();
        var otherTechnicianId = Guid.NewGuid();

        context.Users.AddRange(
            new ApplicationUser
            {
                Id = adminId,
                UserName = "admin@test.local",
                Email = "admin@test.local",
                FullName = "Facilities Admin"
            },
            new ApplicationUser
            {
                Id = reporterId,
                UserName = "reporter@test.local",
                Email = "reporter@test.local",
                FullName = "Alex Reporter"
            },
            new ApplicationUser
            {
                Id = technicianId,
                UserName = "tech1@test.local",
                Email = "tech1@test.local",
                FullName = "Sam Technician"
            },
            new ApplicationUser
            {
                Id = otherTechnicianId,
                UserName = "tech2@test.local",
                Email = "tech2@test.local",
                FullName = "Other Technician"
            });
        context.SaveChanges();

        var service = CreateService(context);
        return new TestFixture(dbName, context, service, adminId, reporterId, technicianId, otherTechnicianId);
    }

    private static TicketService CreateService(ApplicationDbContext context)
    {
        var storage = new TestFileStorageService();
        var duplicateService = new DuplicateDetectionService(context);
        var notificationService = new TestNotificationService();
        return new TicketService(context, storage, duplicateService, notificationService);
    }

    private static async Task<TicketDto> CreateSampleTicketAsync(TestFixture fixture)
    {
        return await fixture.Service.CreateTicketAsync(fixture.ReporterId, new CreateTicketRequest
        {
            Title = "Steam pipe leak in basement",
            Category = "Plumbing",
            Building = "Science Hall",
            Floor = "B1",
            Room = "Utility 02",
            Description = "Hot water dripping near main shutoff valve",
            Priority = TicketPriority.High
        });
    }

    [Fact]
    public async Task AddInternalNoteAsync_AdminAddsMultipleNotes_PersistsAcrossFreshContextWithoutLeakingToCommentsOrHistory()
    {
        var fixture = CreateFixture();
        var ticket = await CreateSampleTicketAsync(fixture);
        var initialUpdatedAt = ticket.UpdatedAt;

        await Task.Delay(15);

        var afterFirst = await fixture.Service.AddInternalNoteAsync(
            ticket.Id,
            fixture.AdminId,
            isAdmin: true,
            new AddNoteRequest { Note = "  Ordered replacement shutoff valve #44B  " });

        Assert.Single(afterFirst.InternalNotes);
        Assert.Contains("Facilities Admin", afterFirst.InternalNotes[0]);
        Assert.Contains("Ordered replacement shutoff valve #44B", afterFirst.InternalNotes[0]);
        Assert.True(afterFirst.UpdatedAt >= initialUpdatedAt);

        // Submit duplicate note text in the same minute to verify both entries persist
        var afterSecond = await fixture.Service.AddInternalNoteAsync(
            ticket.Id,
            fixture.AdminId,
            isAdmin: true,
            new AddNoteRequest { Note = "Ordered replacement shutoff valve #44B" });

        Assert.Equal(2, afterSecond.InternalNotes.Count);
        Assert.Empty(afterSecond.Comments);

        // Verify persistence from a fresh DbContext instance
        var freshOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: fixture.DbName)
            .Options;
        await using var freshContext = new ApplicationDbContext(freshOptions);
        var freshService = CreateService(freshContext);

        var reloaded = await freshService.GetTicketByIdAsync(ticket.Id, fixture.AdminId, isAdmin: true);
        Assert.Equal(2, reloaded.InternalNotes.Count);
        Assert.All(reloaded.InternalNotes, note => Assert.Contains("Ordered replacement shutoff valve #44B", note));

        // Verify note never leaks to public comments or ticket history
        Assert.Empty(await freshContext.TicketComments.Where(c => c.TicketId == ticket.Id).ToListAsync());
        var history = await freshService.GetTicketHistoryAsync(ticket.Id, fixture.ReporterId, isAdmin: false);
        Assert.DoesNotContain(history, h => h.Note != null && h.Note.Contains("shutoff valve #44B"));
    }

    [Fact]
    public async Task GetTicketByIdAsync_RedactsInternalNotesForReporter_ExposesToAdminAndAssignedTechnician()
    {
        var fixture = CreateFixture();
        var ticket = await CreateSampleTicketAsync(fixture);

        fixture.Context.TicketAssignments.Add(new TicketAssignment
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            TechnicianId = fixture.TechnicianId,
            AssignedById = fixture.AdminId,
            AssignedAt = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.AddInternalNoteAsync(
            ticket.Id,
            fixture.AdminId,
            isAdmin: true,
            new AddNoteRequest { Note = "Gate code is 8492# for mechanical room" });

        var adminView = await fixture.Service.GetTicketByIdAsync(ticket.Id, fixture.AdminId, isAdmin: true);
        Assert.Single(adminView.InternalNotes);
        Assert.Contains("8492#", adminView.InternalNotes[0]);

        var assignedTechView = await fixture.Service.GetTicketByIdAsync(ticket.Id, fixture.TechnicianId, isAdmin: false);
        Assert.Single(assignedTechView.InternalNotes);
        Assert.Contains("8492#", assignedTechView.InternalNotes[0]);

        var reporterView = await fixture.Service.GetTicketByIdAsync(ticket.Id, fixture.ReporterId, isAdmin: false);
        Assert.Empty(reporterView.InternalNotes);

        // Unassigned technician still cannot view ticket
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Service.GetTicketByIdAsync(ticket.Id, fixture.OtherTechnicianId, isAdmin: false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task AddInternalNoteAsync_EmptyOrWhitespaceNote_ThrowsArgumentException(string invalidNote)
    {
        var fixture = CreateFixture();
        var ticket = await CreateSampleTicketAsync(fixture);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Service.AddInternalNoteAsync(
                ticket.Id,
                fixture.AdminId,
                isAdmin: true,
                new AddNoteRequest { Note = invalidNote }));
    }

    [Fact]
    public async Task AddInternalNoteAsync_NonAdmin_ThrowsUnauthorizedAccessException()
    {
        var fixture = CreateFixture();
        var ticket = await CreateSampleTicketAsync(fixture);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Service.AddInternalNoteAsync(
                ticket.Id,
                fixture.ReporterId,
                isAdmin: false,
                new AddNoteRequest { Note = "Attempted unauthorized note" }));
    }

    [Fact]
    public async Task AddInternalNoteAsync_MissingOrSoftDeletedTicket_ThrowsKeyNotFoundException()
    {
        var fixture = CreateFixture();
        var ticket = await CreateSampleTicketAsync(fixture);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            fixture.Service.AddInternalNoteAsync(
                Guid.NewGuid(),
                fixture.AdminId,
                isAdmin: true,
                new AddNoteRequest { Note = "Missing ticket" }));

        await fixture.Service.DeleteTicketAsync(ticket.Id, fixture.AdminId);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            fixture.Service.AddInternalNoteAsync(
                ticket.Id,
                fixture.AdminId,
                isAdmin: true,
                new AddNoteRequest { Note = "Deleted ticket" }));
    }

    [Fact]
    public async Task TicketsController_AddInternalNote_IsAdminOnlyAndReturnsUpdatedTicketDetails()
    {
        var fixture = CreateFixture();
        var ticket = await CreateSampleTicketAsync(fixture);

        var method = typeof(TicketsController).GetMethod(nameof(TicketsController.AddInternalNote));
        var authorizeAttr = method?.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorizeAttr);
        Assert.Equal("Admin", authorizeAttr.Roles);

        var controller = new TicketsController(fixture.Service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, fixture.AdminId.ToString()),
                        new Claim(ClaimTypes.Role, "Admin")
                    }, "TestAuth"))
                }
            }
        };

        var result = await controller.AddInternalNote(ticket.Id, new AddNoteRequest { Note = "ESC-202 part ETA Tuesday" });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var details = Assert.IsType<TicketDetailsDto>(okResult.Value);
        Assert.Single(details.InternalNotes);
        Assert.Contains("ESC-202 part ETA Tuesday", details.InternalNotes[0]);

        var badResult = await controller.AddInternalNote(ticket.Id, new AddNoteRequest { Note = "   " });
        Assert.IsType<BadRequestObjectResult>(badResult);
    }
}

