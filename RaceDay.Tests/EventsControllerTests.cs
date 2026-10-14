using Microsoft.AspNetCore.Mvc;
using RaceDay.API.Controllers;
using RaceDay.API.DTOs;
using RaceDay.API.Models;
using RaceDay.Tests.Helpers;
using Xunit;

namespace RaceDay.Tests;

/// <summary>
/// Tests for all /api/events endpoints.
/// Covers: public GET, Organiser CRUD, ownership enforcement, enrolment listing.
/// </summary>
public class EventsControllerTests
{
    private record OrganiserSetup(
        EventsController Controller,
        RaceDay.API.Data.RaceDayDbContext Db,
        int OrganiserId,
        int CategoryId,
        int EventId);

    private async Task<OrganiserSetup> SetupOrganiserControllerAsync()
    {
        var db = TestHelpers.CreateInMemoryDb();
        var (organiserId, _, categoryId, eventId) = await TestHelpers.SeedTestDataAsync(db);

        var organiser = db.Organisers.First(o => o.OrganiserID == organiserId);
        var httpContext = TestHelpers.CreateHttpContext(
            userId: organiser.UserID.ToString(),
            role: "Organiser",
            userName: "Test Organiser");

        var controller = new EventsController(db);
        TestHelpers.SetControllerContext(controller, httpContext);

        return new OrganiserSetup(controller, db, organiserId, categoryId, eventId);
    }

    // ── Public GET Tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllEvents_NoAuth_Returns200WithActiveEvents()
    {
        var db = TestHelpers.CreateInMemoryDb();
        await TestHelpers.SeedTestDataAsync(db);
        var controller = new EventsController(db);
        TestHelpers.SetControllerContext(controller, TestHelpers.CreateHttpContext());

        var result = await controller.GetAllEvents();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task GetEvent_ValidId_Returns200WithDetails()
    {
        var db = TestHelpers.CreateInMemoryDb();
        var (_, _, _, eventId) = await TestHelpers.SeedTestDataAsync(db);
        var controller = new EventsController(db);
        TestHelpers.SetControllerContext(controller, TestHelpers.CreateHttpContext());

        var result = await controller.GetEvent(eventId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task GetEvent_InvalidId_Returns404()
    {
        var db = TestHelpers.CreateInMemoryDb();
        var controller = new EventsController(db);
        TestHelpers.SetControllerContext(controller, TestHelpers.CreateHttpContext());

        var result = await controller.GetEvent(9999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── Create Event Tests ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateEvent_ValidOrganiser_Returns201()
    {
        var setup = await SetupOrganiserControllerAsync();

        var result = await setup.Controller.CreateEvent(new CreateEventDto
        {
            CategoryID = setup.CategoryId,
            Name = "New 21km Race",
            Location = "Pretoria",
            EventDate = DateTime.Now.AddDays(60).ToString("yyyy-MM-dd"),
            StartTime = "06:30",
            MaxParticipants = 500,
            EntryFee = 300m
        });

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_InvalidCategoryId_Returns400()
    {
        var setup = await SetupOrganiserControllerAsync();

        var result = await setup.Controller.CreateEvent(new CreateEventDto
        {
            CategoryID = 9999,   // does not exist
            Name = "Phantom Race",
            Location = "Nowhere",
            EventDate = "2026-12-01",
            StartTime = "07:00"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateEvent_BadDateFormat_Returns400()
    {
        var setup = await SetupOrganiserControllerAsync();

        var result = await setup.Controller.CreateEvent(new CreateEventDto
        {
            CategoryID = setup.CategoryId,
            Name = "Bad Date Race",
            Location = "Cape Town",
            EventDate = "not-a-date",
            StartTime = "07:00"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── Update Event Tests ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateEvent_OwningOrganiser_Returns200()
    {
        var setup = await SetupOrganiserControllerAsync();

        var result = await setup.Controller.UpdateEvent(setup.EventId, new UpdateEventDto
        {
            CategoryID = setup.CategoryId,
            Name = "Updated Race Name",
            Location = "Durban",
            EventDate = "2026-11-15",
            StartTime = "06:00",
            MaxParticipants = 200,
            EntryFee = 250m,
            IsActive = true
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_NotOwner_Returns403()
    {
        var db = TestHelpers.CreateInMemoryDb();
        var (_, _, categoryId, eventId) = await TestHelpers.SeedTestDataAsync(db);

        // Create a second organiser who does NOT own the event
        var anotherUser = new User
        {
            FullName = "Another Organiser",
            Email = "another@test.com",
            PasswordHash = "hash",
            Role = "Organiser"
        };
        db.Users.Add(anotherUser);
        await db.SaveChangesAsync();
        db.Organisers.Add(new Organiser { UserID = anotherUser.UserID, Organisation = "Another Club" });
        await db.SaveChangesAsync();

        var httpContext = TestHelpers.CreateHttpContext(
            userId: anotherUser.UserID.ToString(), role: "Organiser");
        var controller = new EventsController(db);
        TestHelpers.SetControllerContext(controller, httpContext);

        var result = await controller.UpdateEvent(eventId, new UpdateEventDto
        {
            CategoryID = categoryId,
            Name = "Hijacked",
            Location = "X",
            EventDate = "2026-12-01",
            StartTime = "07:00"
        });

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
    }

    // ── Delete Event Tests ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteEvent_OwningOrganiser_Returns200()
    {
        var setup = await SetupOrganiserControllerAsync();

        var result = await setup.Controller.DeleteEvent(setup.EventId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_NonExistent_Returns404()
    {
        var setup = await SetupOrganiserControllerAsync();

        var result = await setup.Controller.DeleteEvent(9999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── Event Enrolments Test ─────────────────────────────────────────────────

    [Fact]
    public async Task GetEventEnrolments_OwningOrganiser_Returns200()
    {
        var setup = await SetupOrganiserControllerAsync();

        var result = await setup.Controller.GetEventEnrolments(setup.EventId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }
}
