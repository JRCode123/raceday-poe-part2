using Microsoft.AspNetCore.Mvc;
using RaceDay.API.Controllers;
using RaceDay.API.DTOs;
using RaceDay.API.Models;
using RaceDay.Tests.Helpers;
using Xunit;

namespace RaceDay.Tests;

/// <summary>
/// Tests for POST /api/auth/register and POST /api/auth/login.
/// Covers: successful registration, duplicate email, invalid role,
/// successful login, wrong password, and session creation.
/// </summary>
public class AuthControllerTests
{
    // Creates a fresh controller with in-memory DB and optional session
    private (AuthController controller, RaceDay.API.Data.RaceDayDbContext db)
        CreateController(string? userId = null, string? role = null)
    {
        var db = TestHelpers.CreateInMemoryDb();
        var controller = new AuthController(db);
        var httpContext = TestHelpers.CreateHttpContext(userId, role);
        TestHelpers.SetControllerContext(controller, httpContext);
        return (controller, db);
    }

    // ── Registration Tests ────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidOrganiser_Returns201()
    {
        var (controller, db) = CreateController();
        var dto = new RegisterDto
        {
            FullName = "Sipho Dlamini",
            Email = "sipho@test.com",
            Password = "Password1!",
            Role = "Organiser",
            Organisation = "RaceZA Events"
        };

        var result = await controller.Register(dto);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objResult.StatusCode);
        Assert.Equal(1, db.Users.Count());
        Assert.Equal(1, db.Organisers.Count());

        var savedUser = db.Users.First();
        Assert.Equal("sipho@test.com", savedUser.Email);
        Assert.Equal("Organiser", savedUser.Role);
        // Password must be hashed — never stored in plain text
        Assert.NotEqual("Password1!", savedUser.PasswordHash);
        Assert.StartsWith("$2", savedUser.PasswordHash); // BCrypt prefix
    }

    [Fact]
    public async Task Register_ValidParticipant_Returns201AndCreatesParticipantProfile()
    {
        var (controller, db) = CreateController();
        var dto = new RegisterDto
        {
            FullName = "Lebo Khumalo",
            Email = "lebo@test.com",
            Password = "Password1!",
            Role = "Participant",
            Gender = "Female",
            EmergencyContact = "Mama Khumalo - 082 111 2222"
        };

        var result = await controller.Register(dto);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objResult.StatusCode);
        Assert.Equal(1, db.Participants.Count());
        Assert.Equal("Female", db.Participants.First().Gender);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409Conflict()
    {
        var (controller, db) = CreateController();
        // Seed an existing user with the same email
        db.Users.Add(new User
        {
            FullName = "Existing",
            Email = "taken@test.com",
            PasswordHash = "hash",
            Role = "Participant"
        });
        await db.SaveChangesAsync();

        var result = await controller.Register(new RegisterDto
        {
            FullName = "New User",
            Email = "taken@test.com",
            Password = "Password1!",
            Role = "Participant"
        });

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task Register_OrganiserWithNoOrganisation_Returns400()
    {
        var (controller, _) = CreateController();
        var result = await controller.Register(new RegisterDto
        {
            FullName = "Missing Org",
            Email = "test@test.com",
            Password = "Password1!",
            Role = "Organiser"
            // Organisation is missing — required for Organisers
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── Login Tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_CorrectCredentials_Returns200AndSetsSession()
    {
        var (controller, _) = CreateController();
        await controller.Register(new RegisterDto
        {
            FullName = "Thabo Nkosi",
            Email = "thabo@test.com",
            Password = "MyPassword1!",
            Role = "Participant"
        });

        var result = await controller.Login(new LoginDto
        {
            Email = "thabo@test.com",
            Password = "MyPassword1!"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);

        // Session must contain user identity
        var session = (MockSession)controller.HttpContext.Session;
        Assert.NotNull(session.GetString("UserId"));
        Assert.Equal("Participant", session.GetString("UserRole"));
        Assert.Equal("Thabo Nkosi", session.GetString("UserName"));
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var (controller, _) = CreateController();
        await controller.Register(new RegisterDto
        {
            FullName = "Zanele Sithole",
            Email = "zanele@test.com",
            Password = "CorrectPassword1!",
            Role = "Participant"
        });

        var result = await controller.Login(new LoginDto
        {
            Email = "zanele@test.com",
            Password = "WrongPassword!"
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_NonExistentEmail_Returns401()
    {
        var (controller, _) = CreateController();

        var result = await controller.Login(new LoginDto
        {
            Email = "nobody@test.com",
            Password = "AnyPassword1!"
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_OrganiserUser_SetsOrganiserRoleInSession()
    {
        var (controller, _) = CreateController();
        await controller.Register(new RegisterDto
        {
            FullName = "Fatima Mokoena",
            Email = "fatima@test.com",
            Password = "Password1!",
            Role = "Organiser",
            Organisation = "Cape Sport Collective"
        });

        await controller.Login(new LoginDto
        {
            Email = "fatima@test.com",
            Password = "Password1!"
        });

        var session = (MockSession)controller.HttpContext.Session;
        Assert.Equal("Organiser", session.GetString("UserRole"));
    }

    [Fact]
    public async Task Logout_ClearsSession()
    {
        var (controller, _) = CreateController();
        await controller.Register(new RegisterDto
        {
            FullName = "Test User",
            Email = "test@test.com",
            Password = "Password1!",
            Role = "Participant"
        });
        await controller.Login(new LoginDto
        {
            Email = "test@test.com",
            Password = "Password1!"
        });

        var session = (MockSession)controller.HttpContext.Session;
        Assert.NotNull(session.GetString("UserId")); // confirm logged in

        controller.Logout();

        Assert.Null(session.GetString("UserId"));
        Assert.Null(session.GetString("UserRole"));
    }
}
