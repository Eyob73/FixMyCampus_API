using FixMyCampus.Api.Controllers;
using FixMyCampus.Application.DTOs.Auth;
using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Application.DTOs.Users;
using FixMyCampus.Domain.Entities;
using FixMyCampus.Domain.Enums;
using FixMyCampus.Infrastructure.Persistence;
using FixMyCampus.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Xunit;

namespace FixMyCampus.Tests;

public class ReporterManagementTests
{
    private (ApplicationDbContext Context, UserManager<ApplicationUser> UserManager, RoleManager<IdentityRole<Guid>> RoleManager, ReporterService ReporterService, AuthService AuthService) CreateTestContext()
    {
        var dbName = $"ReporterTestDb_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new ApplicationDbContext(options);

        var userStore = new UserStore<ApplicationUser, IdentityRole<Guid>, ApplicationDbContext, Guid>(context);
        var roleStore = new RoleStore<IdentityRole<Guid>, ApplicationDbContext, Guid>(context);

        var optionsAccessor = Options.Create(new IdentityOptions
        {
            Password = new PasswordOptions
            {
                RequiredLength = 6,
                RequireUppercase = true,
                RequireDigit = true,
                RequireNonAlphanumeric = true
            },
            SignIn = new SignInOptions
            {
                RequireConfirmedAccount = false,
                RequireConfirmedEmail = false
            }
        });

        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var userValidators = new List<IUserValidator<ApplicationUser>> { new UserValidator<ApplicationUser>() };
        var passwordValidators = new List<IPasswordValidator<ApplicationUser>> { new PasswordValidator<ApplicationUser>() };
        var lookupNormalizer = new UpperInvariantLookupNormalizer();
        var errorDescriber = new IdentityErrorDescriber();

        var userManager = new UserManager<ApplicationUser>(
            userStore,
            optionsAccessor,
            passwordHasher,
            userValidators,
            passwordValidators,
            lookupNormalizer,
            errorDescriber,
            null!,
            null!);

        var roleManager = new RoleManager<IdentityRole<Guid>>(
            roleStore,
            new List<IRoleValidator<IdentityRole<Guid>>>(),
            lookupNormalizer,
            errorDescriber,
            null!);

        // Ensure roles exist
        roleManager.CreateAsync(new IdentityRole<Guid>("Admin")).GetAwaiter().GetResult();
        roleManager.CreateAsync(new IdentityRole<Guid>("Technician")).GetAwaiter().GetResult();
        roleManager.CreateAsync(new IdentityRole<Guid>("Reporter")).GetAwaiter().GetResult();

        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Jwt:Key", "A_Very_Long_Secret_Key_For_Testing_Only_32_Characters_Or_More!" },
            { "Jwt:Issuer", "FixMyCampus" },
            { "Jwt:Audience", "FixMyCampusUsers" }
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var reporterService = new ReporterService(context, userManager);
        var authService = new AuthService(userManager, configuration, context);

        return (context, userManager, roleManager, reporterService, authService);
    }

    [Fact]
    public async Task CreateReporter_SuccessfullyCreatesAccount_WithReporterRole()
    {
        var (context, userManager, _, reporterService, _) = CreateTestContext();

        var request = new CreateReporterDto
        {
            FirstName = "Alice",
            LastName = "Johnson",
            Email = "alice@campus.edu",
            Phone = "555-1234",
            Department = "Computer Science"
        };

        var result = await reporterService.CreateReporterAsync(request);

        Assert.NotNull(result);
        Assert.Equal("Alice Johnson", result.Name);
        Assert.Equal("Alice", result.FirstName);
        Assert.Equal("Johnson", result.LastName);
        Assert.Equal("alice@campus.edu", result.Email);
        Assert.Equal("555-1234", result.Phone);
        Assert.Equal("Computer Science", result.Department);
        Assert.Equal("REPORTER", result.Role);
        Assert.Equal("ACTIVE", result.Status);
        Assert.True(result.IsActive);
        Assert.False(result.IsDeleted);

        // Verify in database and Identity
        var dbUser = await userManager.FindByEmailAsync("alice@campus.edu");
        Assert.NotNull(dbUser);
        Assert.True(await userManager.IsInRoleAsync(dbUser, "Reporter"));
        Assert.False(string.IsNullOrEmpty(dbUser.PasswordHash));
        Assert.False(dbUser.IsDeleted);
    }

    [Fact]
    public async Task CreateReporter_DuplicateEmail_ThrowsInvalidOperationException()
    {
        var (_, _, _, reporterService, _) = CreateTestContext();

        var request1 = new CreateReporterDto
        {
            FirstName = "Bob",
            LastName = "Smith",
            Email = "bob@campus.edu"
        };
        await reporterService.CreateReporterAsync(request1);

        var request2 = new CreateReporterDto
        {
            FirstName = "Robert",
            LastName = "Smith",
            Email = "bob@campus.edu"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reporterService.CreateReporterAsync(request2));
    }

    [Fact]
    public async Task GetReporters_ExcludesSoftDeleted_AndSupportsSearchAndFilter()
    {
        var (_, _, _, reporterService, _) = CreateTestContext();

        var rep1 = await reporterService.CreateReporterAsync(new CreateReporterDto
        {
            FirstName = "Charlie",
            LastName = "Brown",
            Email = "charlie@campus.edu",
            Phone = "111-2222",
            Department = "Arts"
        });

        var rep2 = await reporterService.CreateReporterAsync(new CreateReporterDto
        {
            FirstName = "Diana",
            LastName = "Prince",
            Email = "diana@campus.edu",
            Phone = "333-4444",
            Department = "Law"
        });

        var rep3 = await reporterService.CreateReporterAsync(new CreateReporterDto
        {
            FirstName = "Edward",
            LastName = "Norton",
            Email = "edward@campus.edu",
            Phone = "555-6666",
            Department = "Science"
        });

        // Soft delete rep3
        await reporterService.DeleteReporterAsync(rep3.Id);

        // Suspend rep2
        await reporterService.UpdateStatusAsync(rep2.Id, "SUSPENDED");

        // 1. All non-deleted reporters
        var all = await reporterService.GetReportersAsync(new ReporterQueryParameters());
        Assert.Equal(2, all.TotalCount);
        Assert.DoesNotContain(all.Items, r => r.Id == rep3.Id);

        // 2. Filter by status SUSPENDED
        var suspended = await reporterService.GetReportersAsync(new ReporterQueryParameters { Status = "SUSPENDED" });
        Assert.Single(suspended.Items);
        Assert.Equal("Diana Prince", suspended.Items.First().Name);

        // 3. Search by name
        var searchName = await reporterService.GetReportersAsync(new ReporterQueryParameters { Search = "Charlie" });
        Assert.Single(searchName.Items);
        Assert.Equal("charlie@campus.edu", searchName.Items.First().Email);

        // 4. Search by phone
        var searchPhone = await reporterService.GetReportersAsync(new ReporterQueryParameters { Search = "333-4444" });
        Assert.Single(searchPhone.Items);
        Assert.Equal("Diana Prince", searchPhone.Items.First().Name);
    }

    [Fact]
    public async Task UpdateReporter_UpdatesProfileAndPreservesRole()
    {
        var (_, userManager, _, reporterService, _) = CreateTestContext();

        var created = await reporterService.CreateReporterAsync(new CreateReporterDto
        {
            FirstName = "Frank",
            LastName = "Castle",
            Email = "frank@campus.edu",
            Phone = "100-0000",
            Department = "Security"
        });

        var updateReq = new UpdateReporterDto
        {
            FirstName = "Francis",
            LastName = "Castle",
            Email = "francis@campus.edu",
            Phone = "100-9999",
            Department = "Criminology"
        };

        var updated = await reporterService.UpdateReporterAsync(created.Id, updateReq);

        Assert.Equal("Francis Castle", updated.Name);
        Assert.Equal("Francis", updated.FirstName);
        Assert.Equal("francis@campus.edu", updated.Email);
        Assert.Equal("100-9999", updated.Phone);
        Assert.Equal("Criminology", updated.Department);

        var dbUser = await userManager.FindByIdAsync(created.Id.ToString());
        Assert.NotNull(dbUser);
        Assert.Equal("Francis Castle", dbUser.FullName);
        Assert.True(await userManager.IsInRoleAsync(dbUser, "Reporter"));
    }

    [Fact]
    public async Task UpdateReporter_DuplicateEmail_ThrowsInvalidOperationException()
    {
        var (_, _, _, reporterService, _) = CreateTestContext();

        var rep1 = await reporterService.CreateReporterAsync(new CreateReporterDto
        {
            FirstName = "George",
            LastName = "Clark",
            Email = "george@campus.edu"
        });

        var rep2 = await reporterService.CreateReporterAsync(new CreateReporterDto
        {
            FirstName = "Hannah",
            LastName = "Abbott",
            Email = "hannah@campus.edu"
        });

        // Try updating Hannah's email to George's email
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reporterService.UpdateReporterAsync(rep2.Id, new UpdateReporterDto
            {
                FirstName = "Hannah",
                LastName = "Abbott",
                Email = "george@campus.edu"
            }));
    }

    [Fact]
    public async Task UpdateNonReporter_ThroughReporterService_ThrowsInvalidOperationException()
    {
        var (_, userManager, _, reporterService, _) = CreateTestContext();

        var admin = new ApplicationUser
        {
            UserName = "admin@system.local",
            Email = "admin@system.local",
            FullName = "System Administrator",
            Status = "ACTIVE",
            IsActive = true
        };
        await userManager.CreateAsync(admin, "Admin123!");
        await userManager.AddToRoleAsync(admin, "Admin");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reporterService.UpdateReporterAsync(admin.Id, new UpdateReporterDto
            {
                FirstName = "Hacked",
                LastName = "Admin",
                Email = "admin@system.local"
            }));
    }

    [Fact]
    public async Task DeleteReporter_PerformsSoftDelete_AndPreservesRelatedTickets()
    {
        var (context, userManager, _, reporterService, _) = CreateTestContext();

        var rep = await reporterService.CreateReporterAsync(new CreateReporterDto
        {
            FirstName = "Ian",
            LastName = "Malcolm",
            Email = "ian@campus.edu"
        });

        // Add 2 tickets for Ian
        var ticket1 = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Dinosaur in park",
            Category = "Safety",
            Building = "Lab Complex",
            Description = "Fence broken",
            Status = TicketStatus.New,
            ReporterId = rep.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticket2 = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Power outage",
            Category = "Electrical",
            Building = "Lab Complex",
            Description = "Generators offline",
            Status = TicketStatus.InProgress,
            ReporterId = rep.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Tickets.AddRange(ticket1, ticket2);
        await context.SaveChangesAsync();

        // Perform soft delete
        await reporterService.DeleteReporterAsync(rep.Id);

        // Verify reporter is soft deleted
        var dbUser = await userManager.FindByIdAsync(rep.Id.ToString());
        Assert.NotNull(dbUser);
        Assert.True(dbUser.IsDeleted);
        Assert.NotNull(dbUser.DeletedAt);
        Assert.False(dbUser.IsActive);
        Assert.Equal("DEACTIVATED", dbUser.Status);

        // Verify reporter tickets are NOT deleted and still reference the original ReporterId
        var userTickets = await context.Tickets.Where(t => t.ReporterId == rep.Id).ToListAsync();
        Assert.Equal(2, userTickets.Count);
        Assert.Contains(userTickets, t => t.Title == "Dinosaur in park");
        Assert.Contains(userTickets, t => t.Title == "Power outage");

        // Verify soft-deleted reporter is excluded from listings
        var listing = await reporterService.GetReportersAsync(new ReporterQueryParameters());
        Assert.DoesNotContain(listing.Items, r => r.Id == rep.Id);
    }

    [Fact]
    public async Task DeletedReporter_CannotLogin_AndCannotBeReactivatedViaStatus()
    {
        var (_, _, _, reporterService, authService) = CreateTestContext();

        var rep = await reporterService.CreateReporterAsync(new CreateReporterDto
        {
            FirstName = "Jack",
            LastName = "Sparrow",
            Email = "jack@campus.edu",
            Password = "Password123!"
        });

        // Verify can login before delete
        var loginSuccess = await authService.LoginAsync(new LoginRequest
        {
            Email = "jack@campus.edu",
            Password = "Password123!"
        });
        Assert.NotNull(loginSuccess.AccessToken);

        // Soft delete Jack
        await reporterService.DeleteReporterAsync(rep.Id);

        // Login after deletion should be denied
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authService.LoginAsync(new LoginRequest
            {
                Email = "jack@campus.edu",
                Password = "Password123!"
            }));
        Assert.Contains("deleted", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Attempting to reactivate a soft-deleted account via UpdateStatus should throw
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            reporterService.UpdateStatusAsync(rep.Id, "ACTIVE"));
    }

    [Fact]
    public async Task SuspendedReporter_CannotLogin_RestoredWhenReactivated()
    {
        var (_, _, _, reporterService, authService) = CreateTestContext();

        var rep = await reporterService.CreateReporterAsync(new CreateReporterDto
        {
            FirstName = "Kelly",
            LastName = "Kapoor",
            Email = "kelly@campus.edu",
            Password = "Password123!"
        });

        // Suspend Kelly
        await reporterService.UpdateStatusAsync(rep.Id, "SUSPENDED");

        // Login should be rejected
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authService.LoginAsync(new LoginRequest
            {
                Email = "kelly@campus.edu",
                Password = "Password123!"
            }));

        // Reactivate Kelly
        await reporterService.UpdateStatusAsync(rep.Id, "ACTIVE");

        // Login should succeed now
        var loginResult = await authService.LoginAsync(new LoginRequest
        {
            Email = "kelly@campus.edu",
            Password = "Password123!"
        });
        Assert.NotNull(loginResult.AccessToken);
    }

    [Fact]
    public async Task GetReporterById_ReturnsDetailsWithTicketHistoryAndStatusCounts()
    {
        var (context, _, _, reporterService, _) = CreateTestContext();

        var rep = await reporterService.CreateReporterAsync(new CreateReporterDto
        {
            FirstName = "Leslie",
            LastName = "Knope",
            Email = "leslie@campus.edu",
            Department = "Parks & Rec"
        });

        // Add tickets
        context.Tickets.AddRange(
            new Ticket
            {
                Id = Guid.NewGuid(),
                Title = "Pothole in park",
                Category = "Grounds",
                Building = "Town Hall",
                Status = TicketStatus.New,
                ReporterId = rep.Id,
                CreatedAt = DateTime.UtcNow
            },
            new Ticket
            {
                Id = Guid.NewGuid(),
                Title = "Broken swing",
                Category = "Grounds",
                Building = "Ramsett Park",
                Status = TicketStatus.Resolved,
                ReporterId = rep.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        );
        await context.SaveChangesAsync();

        var detail = await reporterService.GetReporterByIdAsync(rep.Id);

        Assert.NotNull(detail);
        Assert.Equal("Leslie Knope", detail.Name);
        Assert.Equal(2, detail.SubmittedTicketsCount);
        Assert.Equal(1, detail.OpenTicketsCount);
        Assert.Equal(1, detail.NewTicketsCount);
        Assert.Equal(1, detail.ResolvedTicketsCount);
        Assert.Equal(2, detail.Tickets.Count);
        Assert.Contains(detail.Tickets, t => t.Title == "Pothole in park");
    }

    [Fact]
    public async Task GetStats_CalculatesActiveSuspendedDeactivatedCorrectly()
    {
        var (_, _, _, reporterService, _) = CreateTestContext();

        var r1 = await reporterService.CreateReporterAsync(new CreateReporterDto { FirstName = "User1", LastName = "Test", Email = "u1@test.com" });
        var r2 = await reporterService.CreateReporterAsync(new CreateReporterDto { FirstName = "User2", LastName = "Test", Email = "u2@test.com" });
        var r3 = await reporterService.CreateReporterAsync(new CreateReporterDto { FirstName = "User3", LastName = "Test", Email = "u3@test.com" });
        var r4 = await reporterService.CreateReporterAsync(new CreateReporterDto { FirstName = "User4", LastName = "Test", Email = "u4@test.com" });

        await reporterService.UpdateStatusAsync(r2.Id, "SUSPENDED");
        await reporterService.UpdateStatusAsync(r3.Id, "DEACTIVATED");
        await reporterService.DeleteReporterAsync(r4.Id); // Deleted accounts must not be counted

        var stats = await reporterService.GetStatsAsync();

        Assert.Equal(3, stats.TotalReporters); // r1, r2, r3 (r4 is deleted)
        Assert.Equal(1, stats.ActiveReporters);
        Assert.Equal(1, stats.SuspendedReporters);
        Assert.Equal(1, stats.DeactivatedReporters);
    }

    [Fact]
    public async Task AdminController_Endpoints_PerformFullCrudLifecycle()
    {
        var (_, _, _, reporterService, _) = CreateTestContext();

        var controller = new AdminController(null!, null!, reporterService);
        var adminId = Guid.NewGuid();
        var adminUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        }, "TestAuth"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = adminUser }
        };

        // 1. Create Reporter via Controller
        var createResult = await controller.CreateReporter(new CreateReporterDto
        {
            FirstName = "Michael",
            LastName = "Scott",
            Email = "mscott@dundermifflin.com",
            Phone = "555-0199",
            Department = "Management"
        });
        var createdAction = Assert.IsType<CreatedAtActionResult>(createResult);
        var createdDto = Assert.IsType<ReporterDto>(createdAction.Value);
        Assert.Equal("Michael Scott", createdDto.Name);

        // 2. Duplicate Create should return 409 Conflict
        var dupResult = await controller.CreateReporter(new CreateReporterDto
        {
            FirstName = "Michael",
            LastName = "Scarn",
            Email = "mscott@dundermifflin.com"
        });
        Assert.IsType<ConflictObjectResult>(dupResult);

        // 3. Get Reporters Paginated via Controller
        var getResult = await controller.GetReporters(new ReporterQueryParameters());
        var okGet = Assert.IsType<OkObjectResult>(getResult);
        var paginated = Assert.IsType<PaginatedResult<ReporterDto>>(okGet.Value);
        Assert.Single(paginated.Items);

        // 4. Get Reporter Stats via Controller
        var statsResult = await controller.GetReporterStats();
        var okStats = Assert.IsType<OkObjectResult>(statsResult);
        var stats = Assert.IsType<ReporterStatsDto>(okStats.Value);
        Assert.Equal(1, stats.TotalReporters);
        Assert.Equal(1, stats.ActiveReporters);

        // 5. Get Reporter By Id via Controller
        var getByIdResult = await controller.GetReporterById(createdDto.Id);
        var okById = Assert.IsType<OkObjectResult>(getByIdResult);
        var detailDto = Assert.IsType<ReporterDetailDto>(okById.Value);
        Assert.Equal("mscott@dundermifflin.com", detailDto.Email);

        // 6. Update Reporter via Controller
        var updateResult = await controller.UpdateReporter(createdDto.Id, new UpdateReporterDto
        {
            FirstName = "Michael",
            LastName = "Scarn",
            Email = "mscarn@dundermifflin.com",
            Phone = "555-0198",
            Department = "Threat Level Midnight"
        });
        var okUpdate = Assert.IsType<OkObjectResult>(updateResult);
        var updatedDto = Assert.IsType<ReporterDto>(okUpdate.Value);
        Assert.Equal("Michael Scarn", updatedDto.Name);
        Assert.Equal("mscarn@dundermifflin.com", updatedDto.Email);

        // 7. Update Status via Controller
        var statusResult = await controller.UpdateReporterStatus(createdDto.Id, new UpdateReporterStatusDto
        {
            Status = "SUSPENDED"
        });
        var okStatus = Assert.IsType<OkObjectResult>(statusResult);
        var statusDto = Assert.IsType<ReporterDto>(okStatus.Value);
        Assert.Equal("SUSPENDED", statusDto.Status);
        Assert.False(statusDto.IsActive);

        // 8. Soft Delete via Controller
        var deleteResult = await controller.DeleteReporter(createdDto.Id);
        Assert.IsType<NoContentResult>(deleteResult);

        // 9. Subsequent Get By Id should return 404 NotFound
        var deletedGetResult = await controller.GetReporterById(createdDto.Id);
        Assert.IsType<NotFoundObjectResult>(deletedGetResult);
    }

    [Fact]
    public async Task TicketsController_GetTickets_WhenCallerIsReporter_ReturnsOnlyReportersOwnTickets()
    {
        var (context, _, _, _, _) = CreateTestContext();
        var storage = new TestFileStorageService();
        var duplicateService = new DuplicateDetectionService(context);
        var notificationService = new TestNotificationService();
        var ticketService = new TicketService(context, storage, duplicateService, notificationService);

        var reporterAId = Guid.NewGuid();
        var reporterBId = Guid.NewGuid();

        var userA = new ApplicationUser { Id = reporterAId, UserName = "repa@campus.edu", Email = "repa@campus.edu", FullName = "Reporter A" };
        var userB = new ApplicationUser { Id = reporterBId, UserName = "repb@campus.edu", Email = "repb@campus.edu", FullName = "Reporter B" };
        context.Users.AddRange(userA, userB);

        var ticketA1 = new Ticket { Id = Guid.NewGuid(), ReporterId = reporterAId, Title = "Ticket A1", Category = "HVAC", Building = "Hall A", Description = "AC leaking", Status = TicketStatus.New, CreatedAt = DateTime.UtcNow };
        var ticketA2 = new Ticket { Id = Guid.NewGuid(), ReporterId = reporterAId, Title = "Ticket A2", Category = "Electrical", Building = "Hall A", Description = "Outlet broken", Status = TicketStatus.InProgress, CreatedAt = DateTime.UtcNow };
        var ticketB1 = new Ticket { Id = Guid.NewGuid(), ReporterId = reporterBId, Title = "Ticket B1", Category = "Plumbing", Building = "Hall B", Description = "Sink clogged", Status = TicketStatus.Resolved, CreatedAt = DateTime.UtcNow };

        context.Tickets.AddRange(ticketA1, ticketA2, ticketB1);
        await context.SaveChangesAsync();

        var controller = new TicketsController(ticketService);
        var reporterClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, reporterAId.ToString()),
            new Claim(ClaimTypes.Role, "Reporter")
        }, "mock"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = reporterClaims }
        };

        // Act - Call GetTickets (which is /api/tickets)
        var result = await controller.GetTickets(new TicketFilterRequest());

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedTickets = Assert.IsAssignableFrom<IReadOnlyList<TicketDto>>(okResult.Value);

        Assert.Equal(2, returnedTickets.Count);
        Assert.All(returnedTickets, t => Assert.Equal("repa@campus.edu", t.Reporter?.Email));
        Assert.DoesNotContain(returnedTickets, t => t.Title == "Ticket B1");
    }

    [Fact]
    public async Task TicketsController_GetTickets_WhenCallerIsAdmin_ReturnsAllTickets()
    {
        var (context, _, _, _, _) = CreateTestContext();
        var storage = new TestFileStorageService();
        var duplicateService = new DuplicateDetectionService(context);
        var notificationService = new TestNotificationService();
        var ticketService = new TicketService(context, storage, duplicateService, notificationService);

        var reporterAId = Guid.NewGuid();
        var reporterBId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var userA = new ApplicationUser { Id = reporterAId, UserName = "repa@campus.edu", Email = "repa@campus.edu", FullName = "Reporter A" };
        var userB = new ApplicationUser { Id = reporterBId, UserName = "repb@campus.edu", Email = "repb@campus.edu", FullName = "Reporter B" };
        context.Users.AddRange(userA, userB);

        var ticketA1 = new Ticket { Id = Guid.NewGuid(), ReporterId = reporterAId, Title = "Ticket A1", Category = "HVAC", Building = "Hall A", Description = "AC leaking", Status = TicketStatus.New, CreatedAt = DateTime.UtcNow };
        var ticketB1 = new Ticket { Id = Guid.NewGuid(), ReporterId = reporterBId, Title = "Ticket B1", Category = "Plumbing", Building = "Hall B", Description = "Sink clogged", Status = TicketStatus.Resolved, CreatedAt = DateTime.UtcNow };

        context.Tickets.AddRange(ticketA1, ticketB1);
        await context.SaveChangesAsync();

        var controller = new TicketsController(ticketService);
        var adminClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        }, "mock"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = adminClaims }
        };

        // Act
        var result = await controller.GetTickets(new TicketFilterRequest());

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedTickets = Assert.IsAssignableFrom<IReadOnlyList<TicketDto>>(okResult.Value);

        Assert.Equal(2, returnedTickets.Count);
    }

    [Fact]
    public async Task TicketService_GetReporterDashboard_ReturnsOnlyScopedCounts()
    {
        var (context, _, _, _, _) = CreateTestContext();
        var storage = new TestFileStorageService();
        var duplicateService = new DuplicateDetectionService(context);
        var notificationService = new TestNotificationService();
        var ticketService = new TicketService(context, storage, duplicateService, notificationService);

        var reporterAId = Guid.NewGuid();
        var reporterBId = Guid.NewGuid();

        var userA = new ApplicationUser { Id = reporterAId, UserName = "repa@campus.edu", Email = "repa@campus.edu", FullName = "Reporter A" };
        var userB = new ApplicationUser { Id = reporterBId, UserName = "repb@campus.edu", Email = "repb@campus.edu", FullName = "Reporter B" };
        context.Users.AddRange(userA, userB);

        var ticketA1 = new Ticket { Id = Guid.NewGuid(), ReporterId = reporterAId, Title = "Ticket A1", Category = "HVAC", Building = "Hall A", Description = "AC leaking", Status = TicketStatus.New, CreatedAt = DateTime.UtcNow };
        var ticketA2 = new Ticket { Id = Guid.NewGuid(), ReporterId = reporterAId, Title = "Ticket A2", Category = "Electrical", Building = "Hall A", Description = "Outlet broken", Status = TicketStatus.InProgress, CreatedAt = DateTime.UtcNow };
        var ticketA3 = new Ticket { Id = Guid.NewGuid(), ReporterId = reporterAId, Title = "Ticket A3 Deleted", Category = "Electrical", Building = "Hall A", Description = "Deleted", Status = TicketStatus.New, IsDeleted = true, DeletedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
        var ticketB1 = new Ticket { Id = Guid.NewGuid(), ReporterId = reporterBId, Title = "Ticket B1", Category = "Plumbing", Building = "Hall B", Description = "Sink clogged", Status = TicketStatus.Resolved, CreatedAt = DateTime.UtcNow };

        context.Tickets.AddRange(ticketA1, ticketA2, ticketA3, ticketB1);
        await context.SaveChangesAsync();

        // Act
        var statsA = await ticketService.GetReporterDashboardAsync(reporterAId);

        // Assert - only user A's non-deleted tickets counted
        Assert.Equal(2, statsA.TotalTickets);
        Assert.Equal(1, statsA.NewTickets);
        Assert.Equal(1, statsA.InProgressTickets);
        Assert.Equal(0, statsA.ResolvedTickets);
    }

    [Fact]
    public async Task AdminController_Technician_FullLifecycle_CrudAndStatusManagement()
    {
        var (context, userManager, _, reporterService, _) = CreateTestContext();
        var storage = new TestFileStorageService();
        var duplicateService = new DuplicateDetectionService(context);
        var notificationService = new TestNotificationService();
        var ticketService = new TicketService(context, storage, duplicateService, notificationService);
        var adminService = new AdminService(context, userManager, ticketService, notificationService);

        var controller = new AdminController(adminService, ticketService, reporterService);
        var adminId = Guid.NewGuid();
        var adminUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        }, "TestAuth"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = adminUser }
        };

        // 1. Create Technician via Controller
        var createResult = await controller.CreateTechnician(new CreateTechnicianDto
        {
            Name = "Dwight Schrute",
            Email = "dwight@dundermifflin.com",
            Phone = "555-0101",
            Department = "Structural & Carpentry",
            Specialty = "Security & Maintenance",
            Status = "AVAILABLE"
        });
        var createdAction = Assert.IsType<CreatedAtActionResult>(createResult);
        var createdDto = Assert.IsType<TechnicianDto>(createdAction.Value);
        Assert.Equal("Dwight Schrute", createdDto.Name);
        Assert.Equal("dwight@dundermifflin.com", createdDto.Email);
        Assert.Equal("AVAILABLE", createdDto.Status);

        // 2. Email uniqueness check
        var dupResult = await controller.CreateTechnician(new CreateTechnicianDto
        {
            Name = "Dwight Schrute 2",
            Email = "dwight@dundermifflin.com",
            Phone = "555-0102",
            Department = "Structural & Carpentry",
            Specialty = "Security & Maintenance",
            Status = "AVAILABLE"
        });
        Assert.IsType<ConflictObjectResult>(dupResult);

        // 3. Get Technicians Paginated via Controller
        var getResult = await controller.GetTechnicians(new TechnicianQueryParameters { Search = "Dwight" });
        var okGet = Assert.IsType<OkObjectResult>(getResult);
        var paginated = Assert.IsType<PaginatedResult<TechnicianDto>>(okGet.Value);
        Assert.Single(paginated.Items);

        // 4. Get Technician Stats via Controller
        var statsResult = await controller.GetTechnicianStats();
        var okStats = Assert.IsType<OkObjectResult>(statsResult);
        var stats = Assert.IsType<TechnicianStatsDto>(okStats.Value);
        Assert.True(stats.TotalTechnicians >= 1);
        Assert.True(stats.AvailableTechnicians >= 1);

        // 5. Get Technician By Id via Controller
        var getByIdResult = await controller.GetTechnicianById(createdDto.Id);
        var okById = Assert.IsType<OkObjectResult>(getByIdResult);
        var detailDto = Assert.IsType<TechnicianDetailDto>(okById.Value);
        Assert.Equal("dwight@dundermifflin.com", detailDto.Email);

        // 6. Update Technician via Controller
        var updateResult = await controller.UpdateTechnician(createdDto.Id, new UpdateTechnicianDto
        {
            Name = "Dwight K. Schrute",
            Email = "dwight.schrute@dundermifflin.com",
            Phone = "555-0103",
            Department = "Structural & Carpentry",
            Specialty = "Master Technician & Safety",
            Status = "BUSY"
        });
        var okUpdate = Assert.IsType<OkObjectResult>(updateResult);
        var updatedDto = Assert.IsType<TechnicianDto>(okUpdate.Value);
        Assert.Equal("Dwight K. Schrute", updatedDto.Name);
        Assert.Equal("dwight.schrute@dundermifflin.com", updatedDto.Email);
        Assert.Equal("BUSY", updatedDto.Status);

        // 7. Update Status via Controller
        var statusResult = await controller.UpdateTechnicianStatus(createdDto.Id, new UpdateTechnicianStatusDto
        {
            Status = "ON_CALL"
        });
        var okStatus = Assert.IsType<OkObjectResult>(statusResult);
        var statusDto = Assert.IsType<TechnicianDto>(okStatus.Value);
        Assert.Equal("ON_CALL", statusDto.Status);

        // 8. Soft Delete via Controller
        var deleteResult = await controller.DeleteTechnician(createdDto.Id);
        Assert.IsType<NoContentResult>(deleteResult);

        // 9. Subsequent Get By Id should return 404 NotFound
        var deletedGetResult = await controller.GetTechnicianById(createdDto.Id);
        Assert.IsType<NotFoundObjectResult>(deletedGetResult);
    }
}
