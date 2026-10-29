using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Domain.Entities;
using FixMyCampus.Domain.Enums;
using FixMyCampus.Infrastructure.Persistence;
using FixMyCampus.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace FixMyCampus.Tests;

public class DuplicateDetectionServiceTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    [Theory]
    [InlineData("The projector is not working.", "The classroom projector is broken.", true)]
    [InlineData("The projector is not working.", "Projector stopped working in this room.", true)]
    [InlineData("The classroom projector is broken.", "Projector stopped working in this room.", true)]
    public void ComputeTextSimilarity_RecognizesDifferentWordingsOfSameProblem(string text1, string text2, bool shouldBeConfirmed)
    {
        var similarity = DuplicateDetectionService.ComputeTextSimilarity(text1, text2);
        
        Assert.Equal(shouldBeConfirmed, similarity >= DuplicateDetectionService.ConfirmedThreshold);
    }

    [Fact]
    public void ComputeTextSimilarity_DifferentProblemsInSameLocation_YieldsLowSimilarity()
    {
        var text1 = "The classroom projector is not working.";
        var text2 = "The window latch is stuck and won't lock.";

        var similarity = DuplicateDetectionService.ComputeTextSimilarity(text1, text2);

        Assert.True(similarity < DuplicateDetectionService.PotentialThreshold,
            $"Expected distinct problems to have similarity < {DuplicateDetectionService.PotentialThreshold}, but was {similarity}");
    }

    [Fact]
    public void ComputeTextSimilarity_PartialOverlap_YieldsPotentialMatch()
    {
        var text1 = "The classroom projector is broken.";
        var text2 = "HDMI connection cable for classroom projector is missing from podium.";

        var similarity = DuplicateDetectionService.ComputeTextSimilarity(text1, text2);

        Assert.True(similarity >= DuplicateDetectionService.PotentialThreshold,
            $"Expected partial match to have similarity >= {DuplicateDetectionService.PotentialThreshold}, but was {similarity}");
    }

    [Theory]
    [InlineData("Room 101", "101", true)]
    [InlineData("rm 101", "101", true)]
    [InlineData("room 101", "Room 102", false)]
    [InlineData("101", "205", false)]
    [InlineData("Room-302A", "302a", true)]
    public void NormalizeRoom_CorrectlyComparesRoomIdentifiers(string room1, string room2, bool expectedMatch)
    {
        var norm1 = DuplicateDetectionService.NormalizeRoom(room1);
        var norm2 = DuplicateDetectionService.NormalizeRoom(room2);

        Assert.Equal(expectedMatch, string.Equals(norm1, norm2, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CheckDuplicatesAsync_WhenActiveDuplicateExists_ReturnsConfirmedDuplicate()
    {
        using var context = CreateDbContext();
        var service = new DuplicateDetectionService(context);

        var existingTicket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "The projector is not working",
            Description = "Classroom projector will not turn on during lecture",
            Category = "IT_SECURITY",
            Building = "Science Hall",
            Floor = "1",
            Room = "101",
            Status = TicketStatus.New,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Tickets.Add(existingTicket);
        await context.SaveChangesAsync();

        var request = new DuplicateCheckRequest
        {
            Title = "The classroom projector is broken",
            Description = "Projector stopped working in this room",
            Category = "IT_SECURITY",
            Building = "Science Hall",
            Floor = "1",
            Room = "Room 101"
        };

        var response = await service.CheckDuplicatesAsync(request);

        Assert.True(response.IsDuplicate);
        Assert.NotNull(response.PrimaryMatch);
        Assert.Equal("Confirmed", response.PrimaryMatch.MatchLevel);
        Assert.Equal(existingTicket.Id, response.PrimaryMatch.TicketId);
        Assert.Contains("already exists", response.Message);
    }

    [Fact]
    public async Task CheckDuplicatesAsync_WhenPreviousTicketIsResolved_DoesNotBlockNewReport()
    {
        using var context = CreateDbContext();
        var service = new DuplicateDetectionService(context);

        var resolvedTicket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "The projector is not working",
            Description = "Classroom projector will not turn on",
            Category = "IT_SECURITY",
            Building = "Science Hall",
            Floor = "1",
            Room = "101",
            Status = TicketStatus.Resolved, // Terminal status
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        context.Tickets.Add(resolvedTicket);
        await context.SaveChangesAsync();

        var request = new DuplicateCheckRequest
        {
            Title = "The projector is not working",
            Description = "Classroom projector will not turn on",
            Category = "IT_SECURITY",
            Building = "Science Hall",
            Floor = "1",
            Room = "101"
        };

        var response = await service.CheckDuplicatesAsync(request);

        Assert.False(response.IsDuplicate);
        Assert.False(response.HasPotentialDuplicates);
        Assert.Empty(response.Matches);
    }
}
