using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.DTOs;
using RaceDay.API.Helpers;
using RaceDay.API.Models;

namespace RaceDay.API.Controllers;

/// <summary>
/// Handles user registration and login.
/// On successful login, the user's ID and role are stored in the server-side session.
/// The session cookie (set by ASP.NET Core automatically) is then sent with every
/// subsequent request, keeping the user authenticated.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public AuthController(RaceDayDbContext db)
    {
        _db = db;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/auth/register
    // Creates a new user account for either an Organiser or a Participant.
    // Passwords are hashed with BCrypt before saving.
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Register a new Organiser or Participant account.</summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // 409 — email already exists
        if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
            return Conflict(new { message = "An account with this email already exists." });

        // Validate role-specific required fields
        if (dto.Role == "Organiser" && string.IsNullOrWhiteSpace(dto.Organisation))
            return BadRequest(new { message = "Organisation name is required for Organisers." });

        // 1. Create the base User record
        var user = new User
        {
            FullName = dto.FullName,
            Email = dto.Email.ToLowerInvariant(),
            PasswordHash = PasswordHelper.Hash(dto.Password),
            Role = dto.Role
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();   // save to get the generated UserID

        // 2. Create the role-specific profile record
        if (dto.Role == "Organiser")
        {
            _db.Organisers.Add(new Organiser
            {
                UserID = user.UserID,
                Organisation = dto.Organisation!,
                ContactPhone = dto.ContactPhone
            });
        }
        else // Participant
        {
            DateOnly? dob = null;
            if (!string.IsNullOrWhiteSpace(dto.DateOfBirth) &&
                DateOnly.TryParse(dto.DateOfBirth, out var parsedDob))
                dob = parsedDob;

            _db.Participants.Add(new Participant
            {
                UserID = user.UserID,
                DateOfBirth = dob,
                Gender = dto.Gender,
                EmergencyContact = dto.EmergencyContact
            });
        }
        await _db.SaveChangesAsync();

        return StatusCode(201, new
        {
            userId = user.UserID,
            email = user.Email,
            role = user.Role,
            message = "Account created successfully."
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/auth/login
    // Verifies credentials and creates a server-side session on success.
    // The session stores UserId, UserRole, and UserName.
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Log in and start a session. The session cookie is returned automatically.</summary>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == dto.Email.ToLowerInvariant());

        // 401 — wrong email or wrong password
        if (user == null || !PasswordHelper.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        // Store identity in the session (server-side)
        HttpContext.Session.SetString("UserId", user.UserID.ToString());
        HttpContext.Session.SetString("UserRole", user.Role);
        HttpContext.Session.SetString("UserName", user.FullName);

        return Ok(new
        {
            userId = user.UserID,
            fullName = user.FullName,
            email = user.Email,
            role = user.Role,
            message = "Login successful. Session started."
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/auth/logout
    // Clears the session — the user is now unauthenticated.
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Log out and destroy the current session.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new { message = "Logged out successfully." });
    }
}
