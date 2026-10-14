using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.DTOs;
using RaceDay.API.Filters;
using RaceDay.API.Models;

namespace RaceDay.API.Controllers;

/// <summary>
/// Manages RaceDay events.
/// - Public: GET all events, GET one event
/// - Organiser only: POST, PUT, DELETE an event; GET enrolments for their event
/// </summary>
[ApiController]
[Route("api/events")]
[Produces("application/json")]
public class EventsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public EventsController(RaceDayDbContext db)
    {
        _db = db;
    }

    private int GetSessionUserId()
        => int.Parse(HttpContext.Session.GetString("UserId")!);

    // Helper: get the Organiser profile for the logged-in user
    private async Task<Organiser?> GetCurrentOrganiserAsync()
    {
        var userId = GetSessionUserId();
        return await _db.Organisers.FirstOrDefaultAsync(o => o.UserID == userId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/events  —  Public (no login required)
    // Returns all active events, newest first
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Get all active upcoming events. Public — no login needed.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllEvents()
    {
        var events = await _db.Events
            .Include(e => e.Category)
            .Include(e => e.Organiser).ThenInclude(o => o!.User)
            .Where(e => e.IsActive)
            .OrderBy(e => e.EventDate)
            .Select(e => new
            {
                eventId = e.EventID,
                name = e.Name,
                description = e.Description,
                location = e.Location,
                route = e.Route,
                eventDate = e.EventDate.ToString("yyyy-MM-dd"),
                startTime = e.StartTime.ToString("HH:mm"),
                maxParticipants = e.MaxParticipants,
                entryFee = e.EntryFee,
                isActive = e.IsActive,
                category = new { e.Category!.CategoryID, e.Category.Name, e.Category.Sport },
                organiser = new { e.Organiser!.OrganiserID, e.Organiser.Organisation }
            })
            .ToListAsync();

        return Ok(events);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/events/{id}  —  Public
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Get full details of one event by its ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvent(int id)
    {
        var e = await _db.Events
            .Include(e => e.Category)
            .Include(e => e.Organiser).ThenInclude(o => o!.User)
            .FirstOrDefaultAsync(e => e.EventID == id);

        if (e == null) return NotFound(new { message = "Event not found." });

        return Ok(new
        {
            eventId = e.EventID,
            name = e.Name,
            description = e.Description,
            location = e.Location,
            route = e.Route,
            eventDate = e.EventDate.ToString("yyyy-MM-dd"),
            startTime = e.StartTime.ToString("HH:mm"),
            maxParticipants = e.MaxParticipants,
            entryFee = e.EntryFee,
            isActive = e.IsActive,
            category = new { e.Category!.CategoryID, e.Category.Name, e.Category.Sport, e.Category.Description },
            organiser = new
            {
                e.Organiser!.OrganiserID,
                e.Organiser.Organisation,
                e.Organiser.ContactPhone,
                organizerName = e.Organiser.User!.FullName
            },
            enrolmentCount = e.Enrolments.Count
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/events  —  Organiser only
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Create a new event. Linked automatically to the logged-in Organiser.</summary>
    [HttpPost]
    [SessionAuthorize(Role = "Organiser")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var organiser = await GetCurrentOrganiserAsync();
        if (organiser == null)
            return StatusCode(500, new { message = "Organiser profile not found." });

        // Validate date and time
        if (!DateOnly.TryParse(dto.EventDate, out var eventDate))
            return BadRequest(new { message = "Invalid date format. Use YYYY-MM-DD." });
        if (!TimeOnly.TryParse(dto.StartTime, out var startTime))
            return BadRequest(new { message = "Invalid time format. Use HH:mm." });

        // Ensure the category exists
        var categoryExists = await _db.Categories.AnyAsync(c => c.CategoryID == dto.CategoryID);
        if (!categoryExists)
            return BadRequest(new { message = $"Category with ID {dto.CategoryID} does not exist." });

        var raceEvent = new RaceEvent
        {
            OrganiserID = organiser.OrganiserID,
            CategoryID = dto.CategoryID,
            Name = dto.Name,
            Description = dto.Description,
            Location = dto.Location,
            Route = dto.Route,
            EventDate = eventDate,
            StartTime = startTime,
            MaxParticipants = dto.MaxParticipants,
            EntryFee = dto.EntryFee
        };

        _db.Events.Add(raceEvent);
        await _db.SaveChangesAsync();

        return StatusCode(201, new
        {
            eventId = raceEvent.EventID,
            name = raceEvent.Name,
            message = "Event created successfully."
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUT /api/events/{id}  —  Organiser only (own events)
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Update an event. Only the Organiser who created it can edit it.</summary>
    [HttpPut("{id:int}")]
    [SessionAuthorize(Role = "Organiser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEvent(int id, [FromBody] UpdateEventDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var organiser = await GetCurrentOrganiserAsync();
        var raceEvent = await _db.Events.FindAsync(id);

        if (raceEvent == null) return NotFound(new { message = "Event not found." });

        // 403 — only the owning organiser can edit
        if (raceEvent.OrganiserID != organiser?.OrganiserID)
            return StatusCode(403, new { message = "You can only edit your own events." });

        if (!DateOnly.TryParse(dto.EventDate, out var eventDate))
            return BadRequest(new { message = "Invalid date format. Use YYYY-MM-DD." });
        if (!TimeOnly.TryParse(dto.StartTime, out var startTime))
            return BadRequest(new { message = "Invalid time format. Use HH:mm." });

        raceEvent.CategoryID = dto.CategoryID;
        raceEvent.Name = dto.Name;
        raceEvent.Description = dto.Description;
        raceEvent.Location = dto.Location;
        raceEvent.Route = dto.Route;
        raceEvent.EventDate = eventDate;
        raceEvent.StartTime = startTime;
        raceEvent.MaxParticipants = dto.MaxParticipants;
        raceEvent.EntryFee = dto.EntryFee;
        raceEvent.IsActive = dto.IsActive;

        await _db.SaveChangesAsync();
        return Ok(new { message = "Event updated successfully.", eventId = raceEvent.EventID });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DELETE /api/events/{id}  —  Organiser only (own events)
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Delete an event. Only the owning Organiser can delete it.</summary>
    [HttpDelete("{id:int}")]
    [SessionAuthorize(Role = "Organiser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var organiser = await GetCurrentOrganiserAsync();
        var raceEvent = await _db.Events.FindAsync(id);

        if (raceEvent == null) return NotFound(new { message = "Event not found." });
        if (raceEvent.OrganiserID != organiser?.OrganiserID)
            return StatusCode(403, new { message = "You can only delete your own events." });

        _db.Events.Remove(raceEvent);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Event deleted successfully." });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/events/{id}/enrolments  —  Organiser only
    // Returns all participants enrolled in a specific event
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Get all enrolments for a specific event. Organiser only.</summary>
    [HttpGet("{id:int}/enrolments")]
    [SessionAuthorize(Role = "Organiser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventEnrolments(int id)
    {
        var organiser = await GetCurrentOrganiserAsync();
        var raceEvent = await _db.Events.FindAsync(id);

        if (raceEvent == null) return NotFound(new { message = "Event not found." });
        if (raceEvent.OrganiserID != organiser?.OrganiserID)
            return StatusCode(403, new { message = "You can only view enrolments for your own events." });

        var enrolments = await _db.Enrolments
            .Include(en => en.Participant).ThenInclude(p => p!.User)
            .Include(en => en.Result)
            .Where(en => en.EventID == id)
            .Select(en => new
            {
                enrolmentId = en.EnrolmentID,
                participantId = en.ParticipantID,
                participantName = en.Participant!.User!.FullName,
                participantEmail = en.Participant.User.Email,
                enrolledAt = en.EnrolledAt,
                status = en.Status,
                hasResult = en.Result != null,
                finishTime = en.Result != null ? en.Result.FinishTime : null,
                position = en.Result != null ? en.Result.Position : (int?)null
            })
            .ToListAsync();

        return Ok(new
        {
            eventId = id,
            eventName = raceEvent.Name,
            totalEnrolments = enrolments.Count,
            enrolments
        });
    }
}
