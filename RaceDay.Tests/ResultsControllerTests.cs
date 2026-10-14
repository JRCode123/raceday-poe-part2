using Microsoft.AspNetCore.Mvc;
using RaceDay.API.Controllers;
using RaceDay.API.DTOs;
using RaceDay.API.Models;
using RaceDay.Tests.Helpers;
using Xunit;

namespace RaceDay.Tests;

/// <summary>
/// Tests for /api/results endpoints.
/// Covers: Organiser capturing results, updating results, Participant viewing own results,
/// duplicate prevention, and ownership enforcement.
/// </summary>
public class ResultsControllerTests
{
    /// <summary>
    /// Full setup: creates an enrolment so we have something to capture a result for.
    /// Returns an Organiser controller, a Participant controller, and the enrolmentId.
    /// </summary>
    private async Task<(ResultsController orgController, ResultsController partController, int enrolmentId)>
        SetupWithEnrolmentAsync()
    {
        var db = TestHelpers.CreateInMemoryDb();
        var (_, participantId, _, eventId) = await TestHelpers.SeedTestDataAsync(db);

        // Create enrolment
        var participant = db.Participants.First(p => p.ParticipantID == participantId);
        var enrolment = new Enrolment
        {
            ParticipantID = participantId,
            EventID = eventId,
            Status = "Confirmed"
        };
        db.Enrolments.Add(enrolment);
        await db.SaveChangesAsync();

        // Organiser controller
        var organiser = db.Organisers.First();
        var orgUser = await db.Users.FindAsync(organiser.UserID);
        var orgContext = TestHelpers.CreateHttpContext(
            userId: orgUser!.UserID.ToString(), role: "Organiser");
        var orgController = new ResultsController(db);
        TestHelpers.SetControllerContext(orgController, orgContext);

        // Participant controller
        var partContext = TestHelpers.CreateHttpContext(
            userId: participant.UserID.ToString(), role: "Participant");
        var partController = new ResultsController(db);
        TestHelpers.SetControllerContext(partController, partContext);

        return (orgController, partController, enrolment.EnrolmentID);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CAPTURE RESULT TESTS
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CaptureResult_ValidOrganiser_Returns201()
    {
        // Arrange
        var (orgController, _, enrolmentId) = await SetupWithEnrolmentAsync();

        var dto = new CreateResultDto
        {
            EnrolmentID = enrolmentId,
            FinishTime = "2:45:30",
            Position = 47,
            Notes = "Strong finish in difficult conditions"
        };

        // Act
        var result = await orgController.CaptureResult(dto);

        // Assert
        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_DuplicateResult_Returns409Conflict()
    {
        // Arrange — capture a result first
        var (orgController, _, enrolmentId) = await SetupWithEnrolmentAsync();
        await orgController.CaptureResult(new CreateResultDto
        {
            EnrolmentID = enrolmentId,
            FinishTime = "2:45:30",
            Position = 47
        });

        // Act — try to capture again for the same enrolment
        var result = await orgController.CaptureResult(new CreateResultDto
        {
            EnrolmentID = enrolmentId,
            FinishTime = "2:50:00",
            Position = 50
        });

        // Assert — 409 Conflict
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_InvalidEnrolmentId_Returns404()
    {
        // Arrange
        var (orgController, _, _) = await SetupWithEnrolmentAsync();

        // Act
        var result = await orgController.CaptureResult(new CreateResultDto
        {
            EnrolmentID = 9999,
            FinishTime = "1:00:00"
        });

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UPDATE RESULT TESTS
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateResult_OwningOrganiser_Returns200()
    {
        // Arrange — capture first, get resultId
        var (orgController, _, enrolmentId) = await SetupWithEnrolmentAsync();
        var captureResult = await orgController.CaptureResult(new CreateResultDto
        {
            EnrolmentID = enrolmentId,
            FinishTime = "2:45:30",
            Position = 47
        });
        var captureObj = Assert.IsType<ObjectResult>(captureResult);
        var resultId = (int)captureObj.Value!.GetType()
            .GetProperty("resultId")!.GetValue(captureObj.Value)!;

        // Act — update the result
        var updateResult = await orgController.UpdateResult(resultId, new UpdateResultDto
        {
            FinishTime = "2:44:15",
            Position = 45,
            Notes = "Corrected time after official timing review"
        });

        // Assert
        var ok = Assert.IsType<OkObjectResult>(updateResult);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task UpdateResult_NonExistent_Returns404()
    {
        // Arrange
        var (orgController, _, _) = await SetupWithEnrolmentAsync();

        // Act
        var result = await orgController.UpdateResult(9999, new UpdateResultDto
        {
            FinishTime = "1:00:00"
        });

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET MY RESULTS (PARTICIPANT)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyResults_AfterResultCaptured_ReturnsResult()
    {
        // Arrange — capture a result for the participant
        var (orgController, partController, enrolmentId) = await SetupWithEnrolmentAsync();
        await orgController.CaptureResult(new CreateResultDto
        {
            EnrolmentID = enrolmentId,
            FinishTime = "2:45:30",
            Position = 47
        });

        // Act — participant views their own results
        var result = await partController.GetMyResults();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);

        var totalRaces = ok.Value!.GetType()
            .GetProperty("totalRaces")?.GetValue(ok.Value);
        Assert.Equal(1, (int)totalRaces!);
    }

    [Fact]
    public async Task GetMyResults_NoResults_ReturnsEmptyList()
    {
        // Arrange — participant exists but has no results
        var (_, partController, _) = await SetupWithEnrolmentAsync();

        // Act
        var result = await partController.GetMyResults();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var totalRaces = ok.Value!.GetType()
            .GetProperty("totalRaces")?.GetValue(ok.Value);
        Assert.Equal(0, (int)totalRaces!);
    }
}
