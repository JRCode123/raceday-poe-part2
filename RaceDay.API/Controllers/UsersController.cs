using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.DTOs;
using RaceDay.API.Filters;

namespace RaceDay.API.Controllers;

/// <summary>
/// Lets any logged-in user view and update their own profile.
/// Both Organisers and Participants can use these endpoints.
/// </summary>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public UsersController(RaceDayDbContext db)
    {
        _db = db;
    }

    // Helper: read the logged-in user's ID from the session
    private int GetSessionUserId()
        => int.Parse(HttpContext.Session.GetString("UserId")!);

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/users/profile  —  Any logged-in user
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Get the logged-in user's own profile.</summary>
    [HttpGet("profile")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetSessionUserId();
        var role = HttpContext.Session.GetString("UserRole");

        var user = await _db.Users
            .Include(u => u.Organiser)
            .Include(u => u.Participant)
            .FirstOrDefaultAsync(u => u.UserID == userId);

        if (user == null) return NotFound(new { message = "User not found." });

        // Build response — include role-specific fields
        object profile = role == "Organiser"
            ? new
            {
                userId = user.UserID,
                fullName = user.FullName,
                email = user.Email,
                role = user.Role,
                createdAt = user.CreatedAt,
                organisation = user.Organiser?.Organisation,
                contactPhone = user.Organiser?.ContactPhone
            }
            : new
            {
                userId = user.UserID,
                fullName = user.FullName,
                email = user.Email,
                role = user.Role,
                createdAt = user.CreatedAt,
                dateOfBirth = user.Participant?.DateOfBirth?.ToString("yyyy-MM-dd"),
                gender = user.Participant?.Gender,
                emergencyContact = user.Participant?.EmergencyContact
            };

        return Ok(profile);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUT /api/users/profile  —  Any logged-in user
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Update the logged-in user's profile information.</summary>
    [HttpPut("profile")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetSessionUserId();
        var role = HttpContext.Session.GetString("UserRole");

        var user = await _db.Users
            .Include(u => u.Organiser)
            .Include(u => u.Participant)
            .FirstOrDefaultAsync(u => u.UserID == userId);

        if (user == null) return NotFound(new { message = "User not found." });

        user.FullName = dto.FullName;

        if (role == "Organiser" && user.Organiser != null)
        {
            if (!string.IsNullOrWhiteSpace(dto.Organisation))
                user.Organiser.Organisation = dto.Organisation;
            user.Organiser.ContactPhone = dto.ContactPhone;
        }
        else if (role == "Participant" && user.Participant != null)
        {
            user.Participant.EmergencyContact = dto.EmergencyContact;
            user.Participant.Gender = dto.Gender;
        }

        await _db.SaveChangesAsync();

        // Update session name in case it changed
        HttpContext.Session.SetString("UserName", user.FullName);

        return Ok(new { message = "Profile updated successfully.", fullName = user.FullName });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DELETE /api/users/{id}  —  Any logged-in user (own account only)
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Delete own user account. A user cannot delete another user's account.</summary>
    [HttpDelete("{id:int}")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAccount(int id)
    {
        var sessionUserId = GetSessionUserId();

        // 403 — can only delete your own account
        if (id != sessionUserId)
            return StatusCode(403, new { message = "You can only delete your own account." });

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(new { message = "User not found." });

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        HttpContext.Session.Clear();

        return Ok(new { message = "Account deleted successfully." });
    }
}
