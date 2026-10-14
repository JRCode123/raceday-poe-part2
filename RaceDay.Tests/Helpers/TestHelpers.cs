using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.Helpers;
using RaceDay.API.Models;

namespace RaceDay.Tests.Helpers;

/// <summary>
/// Shared helpers used across all test classes.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Creates a fresh in-memory database for each test.
    /// A unique name ensures tests don't interfere with each other.
    /// </summary>
    public static RaceDayDbContext CreateInMemoryDb(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new RaceDayDbContext(options);
    }

    /// <summary>
    /// Creates an HttpContext with a MockSession.
    /// Optionally pre-populates the session with logged-in user data.
    /// </summary>
    public static HttpContext CreateHttpContext(
        string? userId = null,
        string? role = null,
        string? userName = null)
    {
        var httpContext = new DefaultHttpContext();
        var session = new MockSession();

        if (userId != null) session.SetString("UserId", userId);
        if (role != null) session.SetString("UserRole", role);
        if (userName != null) session.SetString("UserName", userName);

        httpContext.Session = session;
        return httpContext;
    }

    /// <summary>
    /// Creates a controller with a fake HTTP context (for direct action method tests).
    /// </summary>
    public static void SetControllerContext(ControllerBase controller, HttpContext httpContext)
    {
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    /// <summary>
    /// Seeds a database with standard test data.
    /// Returns the seeded IDs so tests can reference them.
    /// </summary>
    public static async Task<(int OrganiserId, int ParticipantId, int CategoryId, int EventId)>
        SeedTestDataAsync(RaceDayDbContext db)
    {
        // Organiser user
        var orgUser = new User
        {
            FullName = "Test Organiser",
            Email = "organiser@test.com",
            PasswordHash = PasswordHelper.Hash("Password1!"),
            Role = "Organiser"
        };
        db.Users.Add(orgUser);
        await db.SaveChangesAsync();

        var organiser = new Organiser
        {
            UserID = orgUser.UserID,
            Organisation = "Test Running Club",
            ContactPhone = "011 000 0001"
        };
        db.Organisers.Add(organiser);
        await db.SaveChangesAsync();

        // Participant user
        var partUser = new User
        {
            FullName = "Test Participant",
            Email = "participant@test.com",
            PasswordHash = PasswordHelper.Hash("Password1!"),
            Role = "Participant"
        };
        db.Users.Add(partUser);
        await db.SaveChangesAsync();

        var participant = new Participant
        {
            UserID = partUser.UserID,
            Gender = "Male",
            EmergencyContact = "Emergency Contact - 082 000 0001"
        };
        db.Participants.Add(participant);
        await db.SaveChangesAsync();

        // Category
        var category = new Category
        {
            OrganiserID = organiser.OrganiserID,
            Name = "Road Running",
            Sport = "Running"
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        // Event
        var raceEvent = new RaceEvent
        {
            OrganiserID = organiser.OrganiserID,
            CategoryID = category.CategoryID,
            Name = "Test 10km Race",
            Location = "Johannesburg",
            EventDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            StartTime = new TimeOnly(7, 0),
            MaxParticipants = 100,
            EntryFee = 200.00m,
            IsActive = true
        };
        db.Events.Add(raceEvent);
        await db.SaveChangesAsync();

        return (organiser.OrganiserID, participant.ParticipantID, category.CategoryID, raceEvent.EventID);
    }
}
