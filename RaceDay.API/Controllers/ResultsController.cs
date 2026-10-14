using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.DTOs;
using RaceDay.API.Filters;
using RaceDay.API.Models;

namespace RaceDay.API.Controllers;

/// <summary>
/// Post-race result management.
/// - Organiser only: POST (capture result), PUT (update result)
/// - Participant only: GET /my (view own results across all events)
/// </summary>
[ApiController]
[Route("api/results")]
[Produces("application/json")]
public class ResultsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db)
    {
        _db = db;
    }

    private int GetSessionUserId()
        => int.Parse(HttpContext.Session.GetString("UserId")!);

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/results  —  Organiser only
    // Captures a finish time and position for a specific enrolment
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Capture a result for an enrolled Participant after the race concludes. Organiser only.</summary>
    [HttpPost]
    [SessionAuthorize(Role = "Organiser")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CaptureResult([FromBody] CreateResultDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetSessionUserId();
        var organiser = await _db.Organisers.FirstOrDefaultAsync(o => o.UserID == userId);

        // Get the enrolment and verify the Organiser owns that event
        var enrolment = await _db.Enrolments
            .Include(en => en.Event)
            .Include(en => en.Participant).ThenInclude(p => p!.User)
            .FirstOrDefaultAsync(en => en.EnrolmentID == dto.EnrolmentID);

        if (enrolment == null)
            return NotFound(new { message = "Enrolment not found." });

        // 403 — Organiser can only enter results for their own events
        if (enrolment.Event?.OrganiserID != organiser?.OrganiserID)
            return StatusCode(403, new { message = "You can only capture results for your own events." });

        // 409 — result already exists for this enrolment
        var existing = await _db.Results.AnyAsync(r => r.EnrolmentID == dto.EnrolmentID);
        if (existing)
            return Conflict(new
            {
                message = "A result already exists for this enrolment. Use PUT /api/results/{id} to update it."
            });

        var result = new Result
        {
            EnrolmentID = dto.EnrolmentID,
            FinishTime = dto.FinishTime,
            Position = dto.Position,
            Notes = dto.Notes
        };

        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        return StatusCode(201, new
        {
            resultId = result.ResultID,
            enrolmentId = result.EnrolmentID,
            participantName = enrolment.Participant?.User?.FullName,
            eventName = enrolment.Event?.Name,
            finishTime = result.FinishTime,
            position = result.Position,
            message = "Result captured successfully."
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUT /api/results/{id}  —  Organiser only
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Update an existing result. Organiser only, for their own events.</summary>
    [HttpPut("{id:int}")]
    [SessionAuthorize(Role = "Organiser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateResult(int id, [FromBody] UpdateResultDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetSessionUserId();
        var organiser = await _db.Organisers.FirstOrDefaultAsync(o => o.UserID == userId);

        var result = await _db.Results
            .Include(r => r.Enrolment).ThenInclude(en => en!.Event)
            .FirstOrDefaultAsync(r => r.ResultID == id);

        if (result == null) return NotFound(new { message = "Result not found." });

        if (result.Enrolment?.Event?.OrganiserID != organiser?.OrganiserID)
            return StatusCode(403, new { message = "You can only update results for your own events." });

        result.FinishTime = dto.FinishTime;
        result.Position = dto.Position;
        result.Notes = dto.Notes;

        await _db.SaveChangesAsync();
        return Ok(new { message = "Result updated successfully.", resultId = result.ResultID });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/results/my  —  Participant only
    // A Participant's full race history across all events they've completed
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Get the logged-in Participant's personal race history.
    /// Shows event name, date, category, finish time, and position for every race.
    /// </summary>
    [HttpGet("my")]
    [SessionAuthorize(Role = "Participant")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyResults()
    {
        var userId = GetSessionUserId();
        var participant = await _db.Participants.FirstOrDefaultAsync(p => p.UserID == userId);

        if (participant == null)
            return StatusCode(500, new { message = "Participant profile not found." });

        var results = await _db.Results
            .Include(r => r.Enrolment)
                .ThenInclude(en => en!.Event)
                    .ThenInclude(e => e!.Category)
            .Where(r => r.Enrolment!.ParticipantID == participant.ParticipantID)
            .OrderByDescending(r => r.Enrolment!.Event!.EventDate)
            .Select(r => new
            {
                resultId = r.ResultID,
                eventName = r.Enrolment!.Event!.Name,
                eventDate = r.Enrolment.Event.EventDate.ToString("yyyy-MM-dd"),
                location = r.Enrolment.Event.Location,
                category = r.Enrolment.Event.Category!.Name,
                sport = r.Enrolment.Event.Category.Sport,
                finishTime = r.FinishTime,
                position = r.Position,
                notes = r.Notes,
                recordedAt = r.RecordedAt
            })
            .ToListAsync();

        return Ok(new
        {
            totalRaces = results.Count,
            results
        });
    }
}
