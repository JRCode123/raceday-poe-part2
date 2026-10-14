using Microsoft.AspNetCore.Mvc;
using RaceDay.API.Controllers;
using RaceDay.API.DTOs;
using RaceDay.API.Models;
using RaceDay.Tests.Helpers;
using Xunit;

namespace RaceDay.Tests;

/// <summary>
/// Tests for /api/enrolments endpoints.
/// Covers: participant enrolment, duplicate prevention, viewing own enrolments,
/// cancellation, capacity management, and cross-participant ownership enforcement.
/// </summary>
public class EnrolmentsControllerTests
{
    private record ParticipantSetup(
        EnrolmentsController Controller,
        RaceDay.API.Data.RaceDayDbContext Db,
        int ParticipantId,
        int EventId);

    private async Task<ParticipantSetup> SetupParticipantControllerAsync()
    {
        var db = TestHelpers.CreateInMemoryDb();
        var (_, participantId, _, eventId) = await TestHelpers.SeedTestDataAsync(db);

        var participant = db.Participants.First(p => p.ParticipantID == participantId);
        var httpContext = TestHelpers.CreateHttpContext(
            userId: participant.UserID.ToString(),
            role: "Participant",
            userName: "Test Participant");

        var controller = new EnrolmentsController(db);
        TestHelpers.SetControllerContext(controller, httpContext);

        return new ParticipantSetup(controller, db, participantId, eventId);
    }

    // ── Enrol Tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task EnrolInEvent_ValidParticipant_Returns201WithConfirmedStatus()
    {
        var setup = await SetupParticipantControllerAsync();

        var result = await setup.Controller.EnrolInEvent(
            new CreateEnrolmentDto { EventID = setup.EventId });

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);

        var status = created.Value!.GetType().GetProperty("status")?.GetValue(created.Value);
        Assert.Equal("Confirmed", status?.ToString());
    }

    [Fact]
    public async Task EnrolInEvent_DuplicateEnrolment_Returns409Conflict()
    {
        var setup = await SetupParticipantControllerAsync();

        // Enrol once
        await setup.Controller.EnrolInEvent(new CreateEnrolmentDto { EventID = setup.EventId });

        // Enrol again — should be rejected
        var result = await setup.Controller.EnrolInEvent(
            new CreateEnrolmentDto { EventID = setup.EventId });

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task EnrolInEvent_NonExistentEvent_Returns404()
    {
        var setup = await SetupParticipantControllerAsync();

        var result = await setup.Controller.EnrolInEvent(
            new CreateEnrolmentDto { EventID = 9999 });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task EnrolInEvent_EventAtCapacity_Returns201WithWaitlistedStatus()
    {
        var db = TestHelpers.CreateInMemoryDb();
        var (_, participantId, _, eventId) = await TestHelpers.SeedTestDataAsync(db);

        // Set max participants to 0 to force waitlist
        var ev = await db.Events.FindAsync(eventId);
        ev!.MaxParticipants = 0;
        await db.SaveChangesAsync();

        var participant = db.Participants.First(p => p.ParticipantID == participantId);
        var httpContext = TestHelpers.CreateHttpContext(
            userId: participant.UserID.ToString(), role: "Participant");
        var controller = new EnrolmentsController(db);
        TestHelpers.SetControllerContext(controller, httpContext);

        var result = await controller.EnrolInEvent(new CreateEnrolmentDto { EventID = eventId });

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
        var status = created.Value!.GetType().GetProperty("status")?.GetValue(created.Value);
        Assert.Equal("Waitlisted", status?.ToString());
    }

    // ── Get My Enrolments Tests ───────────────────────────────────────────────

    [Fact]
    public async Task GetMyEnrolments_AfterEnrolling_ReturnsCorrectCount()
    {
        var setup = await SetupParticipantControllerAsync();
        await setup.Controller.EnrolInEvent(new CreateEnrolmentDto { EventID = setup.EventId });

        var result = await setup.Controller.GetMyEnrolments();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);

        var totalEnrolments = ok.Value!.GetType()
            .GetProperty("totalEnrolments")?.GetValue(ok.Value);
        Assert.Equal(1, (int)totalEnrolments!);
    }

    [Fact]
    public async Task GetMyEnrolments_NoEnrolments_ReturnsZeroCount()
    {
        var setup = await SetupParticipantControllerAsync();

        var result = await setup.Controller.GetMyEnrolments();

        var ok = Assert.IsType<OkObjectResult>(result);
        var totalEnrolments = ok.Value!.GetType()
            .GetProperty("totalEnrolments")?.GetValue(ok.Value);
        Assert.Equal(0, (int)totalEnrolments!);
    }

    // ── Cancel Enrolment Tests ────────────────────────────────────────────────

    [Fact]
    public async Task CancelEnrolment_OwnEnrolment_Returns200()
    {
        // Arrange — enrol, then directly read the enrolment ID from the DB
        var db = TestHelpers.CreateInMemoryDb();
        var (_, participantId, _, eventId) = await TestHelpers.SeedTestDataAsync(db);

        var participant = db.Participants.First(p => p.ParticipantID == participantId);
        // Create enrolment directly in DB
        var enrolment = new Enrolment
        {
            ParticipantID = participantId,
            EventID = eventId,
            Status = "Confirmed"
        };
        db.Enrolments.Add(enrolment);
        await db.SaveChangesAsync();

        var httpContext = TestHelpers.CreateHttpContext(
            userId: participant.UserID.ToString(), role: "Participant");
        var controller = new EnrolmentsController(db);
        TestHelpers.SetControllerContext(controller, httpContext);

        // Act
        var result = await controller.CancelEnrolment(enrolment.EnrolmentID);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task CancelEnrolment_NonExistent_Returns404()
    {
        var setup = await SetupParticipantControllerAsync();

        var result = await setup.Controller.CancelEnrolment(9999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task CancelEnrolment_AnotherParticipantsEnrolment_Returns403()
    {
        // Arrange — participant 1 enrols, participant 2 tries to cancel it
        var db = TestHelpers.CreateInMemoryDb();
        var (_, part1Id, _, eventId) = await TestHelpers.SeedTestDataAsync(db);

        // Create participant 2
        var user2 = new User
        {
            FullName = "Participant Two",
            Email = "part2@test.com",
            PasswordHash = "hash",
            Role = "Participant"
        };
        db.Users.Add(user2);
        await db.SaveChangesAsync();
        var part2 = new Participant { UserID = user2.UserID };
        db.Participants.Add(part2);
        await db.SaveChangesAsync();

        // Enrol participant 1
        var enrolment = new Enrolment
        {
            ParticipantID = part1Id,
            EventID = eventId,
            Status = "Confirmed"
        };
        db.Enrolments.Add(enrolment);
        await db.SaveChangesAsync();

        // Log in as participant 2
        var httpContext = TestHelpers.CreateHttpContext(
            userId: user2.UserID.ToString(), role: "Participant");
        var controller = new EnrolmentsController(db);
        TestHelpers.SetControllerContext(controller, httpContext);

        // Act — participant 2 tries to cancel participant 1's enrolment
        var result = await controller.CancelEnrolment(enrolment.EnrolmentID);

        // Assert — 403 Forbidden
        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
    }
}
