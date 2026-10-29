using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Application.Exceptions;
using FixMyCampus.Domain.Entities;
using FixMyCampus.Domain.Enums;
using FixMyCampus.Infrastructure.Persistence;
using FixMyCampus.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace FixMyCampus.Tests;

public class TicketDuplicateWorkflowTests
{
    private (ApplicationDbContext Context, TicketService Service, TestFileStorageService Storage, Guid ReporterId, Guid OtherReporterId) CreateTestContext()
    {
        var dbName = $"WorkflowTestDb_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new ApplicationDbContext(options);

        var reporterId = Guid.NewGuid();
        var otherReporterId = Guid.NewGuid();

        var reporter = new ApplicationUser
        {
            Id = reporterId,
            UserName = "user1@hackathon.local",
            Email = "user1@hackathon.local",
            FullName = "Reporter One"
        };

        var otherReporter = new ApplicationUser
        {
            Id = otherReporterId,
            UserName = "user2@hackathon.local",
            Email = "user2@hackathon.local",
            FullName = "Reporter Two"
        };

        context.Users.AddRange(reporter, otherReporter);
        context.SaveChanges();

        var storage = new TestFileStorageService();
        var duplicateService = new DuplicateDetectionService(context);
        var ticketService = new TicketService(context, storage, duplicateService);

        return (context, ticketService, storage, reporterId, otherReporterId);
    }

    private static IFormFile CreateFakeFormFile(string filename, string contentType = "image/jpeg", int sizeBytes = 1024)
    {
        var bytes = new byte[Math.Max(sizeBytes, 16)];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        bytes[3] = 0xE0;
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", filename)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    /// <summary>
    /// Scenario 1: A user submits a problem that has not previously been reported: creation succeeds.
    /// </summary>
    [Fact]
    public async Task Scenario1_NewProblem_CreationSucceeds()
    {
        var (context, service, _, reporterId, _) = CreateTestContext();

        var request = new CreateTicketRequest
        {
            Title = "HVAC air conditioner blowing hot air",
            Category = "HVAC",
            Building = "Engineering Hall",
            Floor = "2",
            Room = "204",
            Description = "Air conditioning is blowing warm air instead of cooling the room",
            Priority = TicketPriority.High
        };

        var created = await service.CreateTicketAsync(reporterId, request);

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("New", created.Status);
        Assert.Equal("Engineering Hall", created.Building);

        var dbTicket = await context.Tickets.FindAsync(created.Id);
        Assert.NotNull(dbTicket);
        Assert.Equal(TicketStatus.New, dbTicket.Status);
    }

    /// <summary>
    /// Scenario 2: The same user submits the same active problem again: duplicate blocked.
    /// </summary>
    [Fact]
    public async Task Scenario2_SameUser_SubmitsSameProblemAgain_BlockedWithDuplicateException()
    {
        var (context, service, _, reporterId, _) = CreateTestContext();

        var request1 = new CreateTicketRequest
        {
            Title = "The projector is not working",
            Category = "IT_SECURITY",
            Building = "Science Hall",
            Floor = "1",
            Room = "101",
            Description = "Projector will not turn on during presentation",
            Priority = TicketPriority.Medium
        };

        var ticket1 = await service.CreateTicketAsync(reporterId, request1);
        Assert.NotNull(ticket1);

        var request2 = new CreateTicketRequest
        {
            Title = "The projector is not working",
            Category = "IT_SECURITY",
            Building = "Science Hall",
            Floor = "1",
            Room = "101",
            Description = "Projector will not turn on during presentation",
            Priority = TicketPriority.Medium
        };

        var ex = await Assert.ThrowsAsync<DuplicateTicketException>(() =>
            service.CreateTicketAsync(reporterId, request2));

        Assert.True(ex.IsConfirmed);
        Assert.NotNull(ex.DuplicateTicket);
        Assert.Equal(ticket1.Id, ex.DuplicateTicket.TicketId);
        Assert.Equal("Science Hall", ex.DuplicateTicket.Building);
        Assert.Equal("101", ex.DuplicateTicket.Room);

        // Verify only 1 ticket in database
        Assert.Equal(1, await context.Tickets.CountAsync());
    }

    /// <summary>
    /// Scenario 3: A different user submits the same active problem at the same location: duplicate blocked.
    /// </summary>
    [Fact]
    public async Task Scenario3_DifferentUser_SubmitsSameProblemAtSameLocation_Blocked()
    {
        var (context, service, _, reporterId, otherReporterId) = CreateTestContext();

        var firstRequest = new CreateTicketRequest
        {
            Title = "Broken pipe leaking water under bathroom sink",
            Category = "PLUMBING",
            Building = "Student Center",
            Floor = "1",
            Room = "Restroom A",
            Description = "There is a continuous water leak from pipe under sink",
            Priority = TicketPriority.High
        };

        var firstTicket = await service.CreateTicketAsync(reporterId, firstRequest);
        Assert.NotNull(firstTicket);

        // Different user submits identical problem at same location
        var secondRequest = new CreateTicketRequest
        {
            Title = "Broken pipe leaking water under bathroom sink",
            Category = "PLUMBING",
            Building = "Student Center",
            Floor = "1",
            Room = "Restroom A",
            Description = "Water leaking from pipe under the sink continuously",
            Priority = TicketPriority.High
        };

        var ex = await Assert.ThrowsAsync<DuplicateTicketException>(() =>
            service.CreateTicketAsync(otherReporterId, secondRequest));

        Assert.True(ex.IsConfirmed);
        Assert.Equal(firstTicket.Id, ex.DuplicateTicket.TicketId);

        // Ensure no private reporter data of User 1 is leaked to User 2
        Assert.Equal("Student Center", ex.DuplicateTicket.Building);
        Assert.Equal("Restroom A", ex.DuplicateTicket.Room);
        Assert.Equal(1, await context.Tickets.CountAsync());
    }

    /// <summary>
    /// Scenario 4: Two reports use different wording but describe the same problem: duplicate detected.
    /// Examples:
    /// - "The projector is not working."
    /// - "The classroom projector is broken."
    /// - "Projector stopped working in this room."
    /// </summary>
    [Theory]
    [InlineData("The projector is not working.", "The classroom projector is broken.")]
    [InlineData("The projector is not working.", "Projector stopped working in this room.")]
    [InlineData("The classroom projector is broken.", "Projector stopped working in this room.")]
    public async Task Scenario4_DifferentWording_DescribesSameProblem_DuplicateDetected(string originalText, string newReportText)
    {
        var (_, service, _, reporterId, otherReporterId) = CreateTestContext();

        var original = new CreateTicketRequest
        {
            Title = originalText,
            Description = originalText,
            Category = "IT_SECURITY",
            Building = "Science Hall",
            Floor = "1",
            Room = "101",
            Priority = TicketPriority.Medium
        };

        var originalTicket = await service.CreateTicketAsync(reporterId, original);
        Assert.NotNull(originalTicket);

        var newReport = new CreateTicketRequest
        {
            Title = newReportText,
            Description = newReportText,
            Category = "IT_SECURITY",
            Building = "Science Hall",
            Floor = "1",
            Room = "Room 101",
            Priority = TicketPriority.Medium
        };

        var ex = await Assert.ThrowsAsync<DuplicateTicketException>(() =>
            service.CreateTicketAsync(otherReporterId, newReport));

        Assert.Equal(originalTicket.Id, ex.DuplicateTicket.TicketId);
    }

    /// <summary>
    /// Scenario 5: Two different problems occur at the same location: both can be reported.
    /// </summary>
    [Fact]
    public async Task Scenario5_TwoDifferentProblemsAtSameLocation_BothCanBeReported()
    {
        var (context, service, _, reporterId, otherReporterId) = CreateTestContext();

        var problem1 = new CreateTicketRequest
        {
            Title = "The projector is not working",
            Category = "IT_SECURITY",
            Building = "Science Hall",
            Floor = "1",
            Room = "101",
            Description = "Classroom projector will not power on",
            Priority = TicketPriority.Medium
        };

        var ticket1 = await service.CreateTicketAsync(reporterId, problem1);
        Assert.NotNull(ticket1);

        // Entirely different problem in the same room and same building
        var problem2 = new CreateTicketRequest
        {
            Title = "Window latch broken won't close",
            Category = "STRUCTURAL",
            Building = "Science Hall",
            Floor = "1",
            Room = "101",
            Description = "Window latch is damaged and cold draft is entering room",
            Priority = TicketPriority.Low
        };

        var ticket2 = await service.CreateTicketAsync(otherReporterId, problem2);
        Assert.NotNull(ticket2);
        Assert.NotEqual(ticket1.Id, ticket2.Id);

        Assert.Equal(2, await context.Tickets.CountAsync());
    }

    /// <summary>
    /// Scenario 6: The same problem occurs at different locations: both can be reported.
    /// </summary>
    [Fact]
    public async Task Scenario6_SameProblemAtDifferentLocations_BothCanBeReported()
    {
        var (context, service, _, reporterId, otherReporterId) = CreateTestContext();

        var location1 = new CreateTicketRequest
        {
            Title = "The projector is not working",
            Category = "IT_SECURITY",
            Building = "Science Hall",
            Floor = "1",
            Room = "101",
            Description = "Classroom projector will not turn on",
            Priority = TicketPriority.Medium
        };

        var ticket1 = await service.CreateTicketAsync(reporterId, location1);
        Assert.NotNull(ticket1);

        // Same problem, but in Room 205 (or Arts Hall)
        var location2 = new CreateTicketRequest
        {
            Title = "The projector is not working",
            Category = "IT_SECURITY",
            Building = "Science Hall",
            Floor = "2",
            Room = "205",
            Description = "Classroom projector will not turn on",
            Priority = TicketPriority.Medium
        };

        var ticket2 = await service.CreateTicketAsync(otherReporterId, location2);
        Assert.NotNull(ticket2);
        Assert.NotEqual(ticket1.Id, ticket2.Id);
        Assert.Equal("101", ticket1.Room);
        Assert.Equal("205", ticket2.Room);

        Assert.Equal(2, await context.Tickets.CountAsync());
    }

    /// <summary>
    /// Scenario 7: The previous ticket is resolved: a new report can be created with its own ticket ID.
    /// </summary>
    [Fact]
    public async Task Scenario7_PreviousTicketIsResolved_NewReportCanBeCreated()
    {
        var (context, service, _, reporterId, otherReporterId) = CreateTestContext();

        var initialRequest = new CreateTicketRequest
        {
            Title = "The projector is not working",
            Category = "IT_SECURITY",
            Building = "Science Hall",
            Floor = "1",
            Room = "101",
            Description = "Projector lamp blown",
            Priority = TicketPriority.Medium
        };

        var firstTicketDto = await service.CreateTicketAsync(reporterId, initialRequest);

        // Transition first ticket to Resolved
        var firstTicketEntity = await context.Tickets.FindAsync(firstTicketDto.Id);
        Assert.NotNull(firstTicketEntity);
        firstTicketEntity.Status = TicketStatus.Resolved;
        await context.SaveChangesAsync();

        // Now submit the recurring issue at the same location
        var newRequest = new CreateTicketRequest
        {
            Title = "The projector is not working",
            Category = "IT_SECURITY",
            Building = "Science Hall",
            Floor = "1",
            Room = "101",
            Description = "Projector lamp blown again after repair",
            Priority = TicketPriority.Medium
        };

        var newTicketDto = await service.CreateTicketAsync(otherReporterId, newRequest);

        Assert.NotNull(newTicketDto);
        Assert.NotEqual(firstTicketDto.Id, newTicketDto.Id);
        Assert.Equal(TicketStatus.New.ToString(), newTicketDto.Status);

        // Verify both tickets are in historical record
        var allTickets = await context.Tickets.ToListAsync();
        Assert.Equal(2, allTickets.Count);
        Assert.Contains(allTickets, t => t.Id == firstTicketDto.Id && t.Status == TicketStatus.Resolved);
        Assert.Contains(allTickets, t => t.Id == newTicketDto.Id && t.Status == TicketStatus.New);
    }

    /// <summary>
    /// Scenario 8: The previous ticket is closed: a new report can be created.
    /// </summary>
    [Fact]
    public async Task Scenario8_PreviousTicketIsClosed_NewReportCanBeCreated()
    {
        var (context, service, _, reporterId, _) = CreateTestContext();

        var initialRequest = new CreateTicketRequest
        {
            Title = "Water dripping from bathroom ceiling",
            Category = "PLUMBING",
            Building = "Library",
            Floor = "3",
            Room = "Restroom",
            Description = "Ceiling is dripping water onto the floor",
            Priority = TicketPriority.High
        };

        var firstTicketDto = await service.CreateTicketAsync(reporterId, initialRequest);

        // Transition ticket to Closed
        var ticketEntity = await context.Tickets.FindAsync(firstTicketDto.Id);
        Assert.NotNull(ticketEntity);
        ticketEntity.Status = TicketStatus.Closed;
        await context.SaveChangesAsync();

        // New report for the same problem can now be created
        var newRequest = new CreateTicketRequest
        {
            Title = "Water dripping from bathroom ceiling",
            Category = "PLUMBING",
            Building = "Library",
            Floor = "3",
            Room = "Restroom",
            Description = "Ceiling is dripping water onto the floor again",
            Priority = TicketPriority.High
        };

        var newTicketDto = await service.CreateTicketAsync(reporterId, newRequest);

        Assert.NotNull(newTicketDto);
        Assert.NotEqual(firstTicketDto.Id, newTicketDto.Id);
        Assert.Equal(2, await context.Tickets.CountAsync());
    }

    /// <summary>
    /// Scenario 9: Two users submit the same issue simultaneously: only one active matching ticket is created.
    /// </summary>
    [Fact]
    public async Task Scenario9_TwoUsersSubmitSimultaneously_OnlyOneActiveTicketCreated()
    {
        var (context, service, _, reporterId, otherReporterId) = CreateTestContext();

        var request1 = new CreateTicketRequest
        {
            Title = "The classroom projector is broken",
            Category = "IT_SECURITY",
            Building = "Science Hall",
            Floor = "1",
            Room = "101",
            Description = "Projector stopped working in this room",
            Priority = TicketPriority.High
        };

        var request2 = new CreateTicketRequest
        {
            Title = "The classroom projector is broken",
            Category = "IT_SECURITY",
            Building = "Science Hall",
            Floor = "1",
            Room = "101",
            Description = "Projector stopped working in this room",
            Priority = TicketPriority.High
        };

        // Run both requests concurrently
        var task1 = Task.Run(async () =>
        {
            try { return await service.CreateTicketAsync(reporterId, request1); }
            catch (DuplicateTicketException) { return null; }
        });

        var task2 = Task.Run(async () =>
        {
            try { return await service.CreateTicketAsync(otherReporterId, request2); }
            catch (DuplicateTicketException) { return null; }
        });

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r != null);
        var nullCount = results.Count(r => r == null);

        Assert.Equal(1, successCount);
        Assert.Equal(1, nullCount);
        Assert.Equal(1, await context.Tickets.CountAsync());
    }

    /// <summary>
    /// Scenario 10: Backend validation, permissions, image uploads, and existing ticket workflows continue working correctly.
    /// </summary>
    [Fact]
    public async Task Scenario10_BackendValidation_ImageUploads_AndWorkflowContinueWorking()
    {
        var (context, service, storage, reporterId, _) = CreateTestContext();

        // 1. Image count validation (> 5 throws ArgumentException)
        var tooManyFiles = Enumerable.Range(1, 6)
            .Select(i => CreateFakeFormFile($"photo{i}.jpg"))
            .ToList();

        var invalidRequest = new CreateTicketRequest
        {
            Title = "Test issue with too many images",
            Category = "GENERAL",
            Building = "Main Hall",
            Room = "10",
            Description = "Description with sufficient characters for test"
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateTicketAsync(reporterId, invalidRequest, tooManyFiles));

        // Ensure no files were saved to storage
        Assert.Empty(storage.SavedFiles);

        // 2. Valid ticket creation with images
        var validFiles = new List<IFormFile>
        {
            CreateFakeFormFile("evidence1.jpg"),
            CreateFakeFormFile("evidence2.jpg")
        };

        var validRequest = new CreateTicketRequest
        {
            Title = "Door handle broken and loose",
            Category = "STRUCTURAL",
            Building = "Main Hall",
            Room = "10",
            Description = "The door handle is detached from the frame and will not turn properly",
            Priority = TicketPriority.Medium
        };

        var ticket = await service.CreateTicketAsync(reporterId, validRequest, validFiles);

        Assert.NotNull(ticket);
        Assert.Equal(2, ticket.Images.Count);
        Assert.Equal(2, storage.SavedFiles.Count);

        // Check history was recorded
        var history = await context.TicketHistories.Where(h => h.TicketId == ticket.Id).ToListAsync();
        Assert.Single(history);
        Assert.Equal(TicketStatus.New, history[0].NewStatus);

        // 3. Status transitions continue working
        var updated = await service.UpdateStatusAsync(ticket.Id, reporterId, isAdmin: true, TicketStatus.Assigned, "Assigned to tech");
        Assert.Equal(TicketStatus.Assigned.ToString(), updated.Status);

        var updatedHistory = await context.TicketHistories.Where(h => h.TicketId == ticket.Id).ToListAsync();
        Assert.Equal(2, updatedHistory.Count);

        // 4. Potential duplicate with IgnorePotentialDuplicates flag can be submitted
        var similarRequestWithBypass = new CreateTicketRequest
        {
            Title = "Door keycard scanner near handle has red light",
            Category = "STRUCTURAL",
            Building = "Main Hall",
            Room = "10",
            Description = "Door access reader is showing red light when badge is tapped",
            Priority = TicketPriority.Low,
            IgnorePotentialDuplicates = true
        };

        var bypassedTicket = await service.CreateTicketAsync(reporterId, similarRequestWithBypass);
        Assert.NotNull(bypassedTicket);
        Assert.NotEqual(ticket.Id, bypassedTicket.Id);
    }
}
