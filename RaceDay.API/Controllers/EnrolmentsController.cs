using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.DTOs;
using RaceDay.API.Filters;
using RaceDay.API.Models;

namespace RaceDay.API.Controllers;

/// <summary>
/// Manages event enrolments — Participants entering races.
/// - Participant only: POST (enrol), GET (my enrolments), DELETE (cancel)
/// </summary>
[ApiController]
[Route("api/enrolments")]
[Produces("application/json")]
public class EnrolmentsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db)
    {
        _db = db;
    }

    private int GetSessionUserId()
        => int.Parse(HttpContext.Session.GetString("UserId")!);

    private async Task<Participant?> GetCurrentParticipantAsync()
    {
        var userId = GetSessionUserId();
        return await _db.Participants.FirstOrDefaultAsync(p => p.UserID == userId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/enrolments  —  Participant only
    // A participant enters a specific event
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Enrol in an event. Records the link between the Participant, Event, and selected Category.</summary>
    [HttpPost]
    [SessionAuthorize(Role = "Participant")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EnrolInEvent([FromBody] CreateEnrolmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var participant = await GetCurrentParticipantAsync();
        if (participant == null)
            return StatusCode(500, new { message = "Participant profile not found." });

        // Check the event exists and is active
        var raceEvent = await _db.Events
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.EventID == dto.EventID && e.IsActive);

        if (raceEvent == null)
            return NotFound(new { message = "Event not found or is no longer active." });

        // 409 — already enrolled in this event
        var alreadyEnrolled = await _db.Enrolments.AnyAsync(
            en => en.ParticipantID == participant.ParticipantID && en.EventID == dto.EventID);

        if (alreadyEnrolled)
            return Conflict(new { message = "You are already enrolled in this event." });

        // Check capacity
        var currentEnrolments = await _db.Enrolments
            .CountAsync(en => en.EventID == dto.EventID && en.Status == "Confirmed");

        var status = currentEnrolments >= raceEvent.MaxParticipants ? "Waitlisted" : "Confirmed";

        var enrolment = new Enrolment
        {
            ParticipantID = participant.ParticipantID,
            EventID = dto.EventID,
            Status = status
        };

        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync();

        return StatusCode(201, new
        {
            enrolmentId = enrolment.EnrolmentID,
            eventId = raceEvent.EventID,
            eventName = raceEvent.Name,
            eventDate = raceEvent.EventDate.ToString("yyyy-MM-dd"),
            category = raceEvent.Category?.Name,
            status = enrolment.Status,
            enrolledAt = enrolment.EnrolledAt,
            message = status == "Confirmed"
                ? "Successfully enrolled! See you at the race."
                : "You have been placed on the waitlist — the event is currently full."
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/enrolments/my  —  Participant only
    // Returns all events the logged-in participant is enrolled in
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Get all enrolments for the logged-in Participant, including results.</summary>
    [HttpGet("my")]
    [SessionAuthorize(Role = "Participant")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyEnrolments()
    {
        var participant = await GetCurrentParticipantAsync();
        if (participant == null)
            return StatusCode(500, new { message = "Participant profile not found." });

        var enrolments = await _db.Enrolments
            .Include(en => en.Event).ThenInclude(e => e!.Category)
            .Include(en => en.Result)
            .Where(en => en.ParticipantID == participant.ParticipantID)
            .OrderByDescending(en => en.EnrolledAt)
            .Select(en => new
            {
                enrolmentId = en.EnrolmentID,
                enrolledAt = en.EnrolledAt,
                status = en.Status,
                @event = new
                {
                    eventId = en.Event!.EventID,
                    name = en.Event.Name,
                    eventDate = en.Event.EventDate.ToString("yyyy-MM-dd"),
                    location = en.Event.Location,
                    category = en.Event.Category!.Name,
                    sport = en.Event.Category.Sport
                },
                result = en.Result == null ? null : new
                {
                    finishTime = en.Result.FinishTime,
                    position = en.Result.Position,
                    notes = en.Result.Notes,
                    recordedAt = en.Result.RecordedAt
                }
            })
            .ToListAsync();

        return Ok(new
        {
            participantName = (await _db.Users.FindAsync(participant.UserID))?.FullName,
            totalEnrolments = enrolments.Count,
            enrolments
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DELETE /api/enrolments/{id}  —  Participant only (own enrolments)
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Cancel an enrolment. A Participant can only cancel their own enrolments.</summary>
    [HttpDelete("{id:int}")]
    [SessionAuthorize(Role = "Participant")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelEnrolment(int id)
    {
        var participant = await GetCurrentParticipantAsync();

        var enrolment = await _db.Enrolments.FindAsync(id);
        if (enrolment == null) return NotFound(new { message = "Enrolment not found." });

        // 403 — can only cancel your own enrolment
        if (enrolment.ParticipantID != participant?.ParticipantID)
            return StatusCode(403, new { message = "You can only cancel your own enrolments." });

        _db.Enrolments.Remove(enrolment);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Enrolment cancelled successfully." });
    }
}
