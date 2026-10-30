using FixMyCampus.Application.DTOs.Notifications;
using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Domain.Entities;
using FixMyCampus.Domain.Enums;
using FixMyCampus.Infrastructure.Hubs;
using FixMyCampus.Infrastructure.Persistence;
using FixMyCampus.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FixMyCampus.Tests;

public class TestHubContext : IHubContext<NotificationHub>
{
    public TestHubClients TestClients { get; } = new();
    public IHubClients Clients => TestClients;
    public IGroupManager Groups => throw new NotImplementedException();
}

public class TestHubClients : IHubClients
{
    public List<(string Target, string Method, object?[] Args)> SentMessages { get; } = new();
    public Dictionary<string, TestClientProxy> UserProxies { get; } = new();

    public IClientProxy All => new TestClientProxy(this, "All");
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new TestClientProxy(this, "AllExcept");
    public IClientProxy Client(string connectionId) => new TestClientProxy(this, $"Client:{connectionId}");
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new TestClientProxy(this, "Clients");
    public IClientProxy Group(string groupName) => new TestClientProxy(this, $"Group:{groupName}");
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new TestClientProxy(this, $"GroupExcept:{groupName}");
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => new TestClientProxy(this, "Groups");
    public IClientProxy User(string userId)
    {
        if (!UserProxies.TryGetValue(userId, out var proxy))
        {
            proxy = new TestClientProxy(this, userId);
            UserProxies[userId] = proxy;
        }
        return proxy;
    }
    public IClientProxy Users(IReadOnlyList<string> userIds) => new TestClientProxy(this, "Users");
}

public class TestClientProxy : IClientProxy
{
    private readonly TestHubClients _hubClients;
    public string UserId { get; }
    public List<(string Method, object?[] Args)> Invocations { get; } = new();

    public TestClientProxy(TestHubClients hubClients, string userId)
    {
        _hubClients = hubClients;
        UserId = userId;
    }

    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        Invocations.Add((method, args));
        _hubClients.SentMessages.Add((UserId, method, args));
        return Task.CompletedTask;
    }
}

public class NotificationSystemTests
{
    private (ApplicationDbContext Context, UserManager<ApplicationUser> UserManager, RoleManager<IdentityRole<Guid>> RoleManager, TestHubContext HubContext, NotificationService Service) CreateTestServices()
    {
        var dbName = $"NotifTestDb_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new ApplicationDbContext(options);

        var userStore = new UserStore<ApplicationUser, IdentityRole<Guid>, ApplicationDbContext, Guid>(context);
        var roleStore = new RoleStore<IdentityRole<Guid>, ApplicationDbContext, Guid>(context);

        var roleManager = new RoleManager<IdentityRole<Guid>>(
            roleStore,
            null!,
            null!,
            null!,
            null!);

        var userManager = new UserManager<ApplicationUser>(
            userStore,
            null!,
            new PasswordHasher<ApplicationUser>(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var hubContext = new TestHubContext();
        var logger = NullLogger<NotificationService>.Instance;

        var service = new NotificationService(context, hubContext, userManager, logger);

        return (context, userManager, roleManager, hubContext, service);
    }

    [Fact]
    public async Task CreateAndSendNotificationAsync_PersistsInPostgresAndDeliversViaSignalR()
    {
        var (context, userManager, _, hubContext, service) = CreateTestServices();
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, UserName = "student@campus.edu", Email = "student@campus.edu", FullName = "Jane Doe" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var result = await service.CreateAndSendNotificationAsync(
            userId,
            "Issue Resolved",
            "Your reported issue has been resolved.",
            "IssueResolved");

        Assert.NotNull(result);
        Assert.Equal("Issue Resolved", result.Title);
        Assert.False(result.IsRead);

        // Verify Database Persistence
        var dbNotif = await context.Notifications.FirstOrDefaultAsync(n => n.Id == result.Id);
        Assert.NotNull(dbNotif);
        Assert.Equal(userId, dbNotif.UserId);
        Assert.Equal("Issue Resolved", dbNotif.Title);

        // Verify Real-time SignalR delivery to target user
        Assert.True(hubContext.TestClients.UserProxies.ContainsKey(userId.ToString()));
        var userProxy = hubContext.TestClients.UserProxies[userId.ToString()];
        Assert.Contains(userProxy.Invocations, inv => inv.Method == "ReceiveNotification");
    }

    [Fact]
    public async Task NotifyAdministratorsAsync_DeliversToAllEligibleAdministrators()
    {
        var (context, userManager, roleManager, hubContext, service) = CreateTestServices();

        await roleManager.CreateAsync(new IdentityRole<Guid>("Admin"));

        var admin1 = new ApplicationUser { Id = Guid.NewGuid(), UserName = "admin1@campus.edu", Email = "admin1@campus.edu", FullName = "Admin One" };
        var admin2 = new ApplicationUser { Id = Guid.NewGuid(), UserName = "admin2@campus.edu", Email = "admin2@campus.edu", FullName = "Admin Two" };
        var regularUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user@campus.edu", Email = "user@campus.edu", FullName = "Regular User" };

        await userManager.CreateAsync(admin1);
        await userManager.CreateAsync(admin2);
        await userManager.CreateAsync(regularUser);

        await userManager.AddToRoleAsync(admin1, "Admin");
        await userManager.AddToRoleAsync(admin2, "Admin");

        var ticketId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId,
            Title = "Water Leak",
            Category = "Plumbing",
            Building = "Engineering",
            ReporterId = regularUser.Id,
            Status = TicketStatus.New
        };
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        await service.NotifyAdministratorsAsync(
            "New Issue Reported",
            "Regular User reported a new issue: \"Water Leak\".",
            "NewIssueReported",
            ticketId);

        // Verify both admins got DB notifications
        var admin1Notifs = await context.Notifications.Where(n => n.UserId == admin1.Id).ToListAsync();
        var admin2Notifs = await context.Notifications.Where(n => n.UserId == admin2.Id).ToListAsync();
        var userNotifs = await context.Notifications.Where(n => n.UserId == regularUser.Id).ToListAsync();

        Assert.Single(admin1Notifs);
        Assert.Single(admin2Notifs);
        Assert.Empty(userNotifs); // Regular reporter does NOT receive admin notification

        // Verify SignalR delivery to both admins
        Assert.True(hubContext.TestClients.UserProxies.ContainsKey(admin1.Id.ToString()));
        Assert.True(hubContext.TestClients.UserProxies.ContainsKey(admin2.Id.ToString()));
        Assert.False(hubContext.TestClients.UserProxies.ContainsKey(regularUser.Id.ToString()));
    }

    [Fact]
    public async Task DuplicateNotificationPrevention_DoesNotCreateDuplicateWithinShortWindow()
    {
        var (context, _, _, _, service) = CreateTestServices();
        var userId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        var first = await service.CreateAndSendNotificationAsync(userId, "Status Update", "Ticket is in progress", "IssueInProgress", ticketId);
        var second = await service.CreateAndSendNotificationAsync(userId, "Status Update", "Ticket is in progress", "IssueInProgress", ticketId);

        Assert.Equal(first.Id, second.Id);

        var totalInDb = await context.Notifications.CountAsync(n => n.UserId == userId && n.TicketId == ticketId);
        Assert.Equal(1, totalInDb);
    }

    [Fact]
    public async Task MarkAsReadAsync_UpdatesStatusAndReadAt()
    {
        var (context, _, _, _, service) = CreateTestServices();
        var userId = Guid.NewGuid();

        var notif = await service.CreateAndSendNotificationAsync(userId, "Test Alert", "Message", "General");
        Assert.False(notif.IsRead);

        var marked = await service.MarkAsReadAsync(notif.Id, userId);
        Assert.True(marked);

        var updated = await context.Notifications.FindAsync(notif.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsRead);
        Assert.NotNull(updated.ReadAt);
    }

    [Fact]
    public async Task SecurityCheck_UserCannotAccessOrModifyAnotherUsersNotification()
    {
        var (context, _, _, _, service) = CreateTestServices();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var notifA = await service.CreateAndSendNotificationAsync(userA, "Private Alert", "Confidential", "General");

        // User B tries to mark User A's notification as read
        var readResult = await service.MarkAsReadAsync(notifA.Id, userB);
        Assert.False(readResult);

        // User B tries to delete User A's notification
        var deleteResult = await service.DeleteNotificationAsync(notifA.Id, userB);
        Assert.False(deleteResult);

        // User A's notification remains intact
        var stillExists = await context.Notifications.FindAsync(notifA.Id);
        Assert.NotNull(stillExists);
        Assert.False(stillExists.IsRead);

        // User B's notifications list does NOT include User A's notification
        var userBNotifs = await service.GetUserNotificationsAsync(userB);
        Assert.Empty(userBNotifs);
    }

    [Fact]
    public async Task UnreadCount_CalculatesAccurately()
    {
        var (context, _, _, _, service) = CreateTestServices();
        var userId = Guid.NewGuid();

        await service.CreateAndSendNotificationAsync(userId, "N1", "M1", "T1");
        await service.CreateAndSendNotificationAsync(userId, "N2", "M2", "T2");
        var n3 = await service.CreateAndSendNotificationAsync(userId, "N3", "M3", "T3");

        var countBefore = await service.GetUnreadCountAsync(userId);
        Assert.Equal(3, countBefore);

        await service.MarkAsReadAsync(n3.Id, userId);

        var countAfter = await service.GetUnreadCountAsync(userId);
        Assert.Equal(2, countAfter);

        await service.MarkAllAsReadAsync(userId);

        var countFinal = await service.GetUnreadCountAsync(userId);
        Assert.Equal(0, countFinal);
    }
}
