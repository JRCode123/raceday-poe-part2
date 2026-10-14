using System.ComponentModel.DataAnnotations;

namespace RaceDay.API.DTOs;

// ──────────────────────────────────────────────────────────────────────────────
// AUTH DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Body for POST /api/auth/register
/// </summary>
public class RegisterDto
{
    [Required(ErrorMessage = "Full name is required.")]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Must be "Organiser" or "Participant".</summary>
    [Required(ErrorMessage = "Role is required.")]
    [RegularExpression("^(Organiser|Participant)$",
        ErrorMessage = "Role must be 'Organiser' or 'Participant'.")]
    public string Role { get; set; } = string.Empty;

    // ── Role-specific extra fields ──────────────────────────────────────────
    /// <summary>Required when Role = "Organiser".</summary>
    public string? Organisation { get; set; }
    public string? ContactPhone { get; set; }

    /// <summary>Optional when Role = "Participant".</summary>
    public string? DateOfBirth { get; set; }   // "YYYY-MM-DD"
    public string? Gender { get; set; }         // "Male" | "Female" | "Other"
    public string? EmergencyContact { get; set; }
}

/// <summary>
/// Body for POST /api/auth/login
/// </summary>
public class LoginDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}

// ──────────────────────────────────────────────────────────────────────────────
// USER PROFILE DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Body for PUT /api/users/profile
/// </summary>
public class UpdateProfileDto
{
    [Required(ErrorMessage = "Full name is required.")]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    // Organiser-specific
    public string? Organisation { get; set; }
    public string? ContactPhone { get; set; }

    // Participant-specific
    public string? EmergencyContact { get; set; }
    public string? Gender { get; set; }
}

// ──────────────────────────────────────────────────────────────────────────────
// EVENT DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Body for POST /api/events
/// </summary>
public class CreateEventDto
{
    [Required]
    public int CategoryID { get; set; }

    [Required(ErrorMessage = "Event name is required.")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Location is required.")]
    [MaxLength(200)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Route { get; set; }

    /// <summary>Format: "YYYY-MM-DD"</summary>
    [Required(ErrorMessage = "Event date is required.")]
    public string EventDate { get; set; } = string.Empty;

    /// <summary>Format: "HH:mm"</summary>
    [Required(ErrorMessage = "Start time is required.")]
    public string StartTime { get; set; } = string.Empty;

    public int MaxParticipants { get; set; } = 500;
    public decimal EntryFee { get; set; } = 0.00m;
}

/// <summary>
/// Body for PUT /api/events/{id}
/// </summary>
public class UpdateEventDto
{
    [Required]
    public int CategoryID { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(200)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Route { get; set; }

    [Required]
    public string EventDate { get; set; } = string.Empty;

    [Required]
    public string StartTime { get; set; } = string.Empty;

    public int MaxParticipants { get; set; } = 500;
    public decimal EntryFee { get; set; } = 0.00m;
    public bool IsActive { get; set; } = true;
}

// ──────────────────────────────────────────────────────────────────────────────
// CATEGORY DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Body for POST /api/categories
/// </summary>
public class CreateCategoryDto
{
    [Required(ErrorMessage = "Category name is required.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Sport is required.")]
    [RegularExpression("^(Running|Cycling|Walking)$",
        ErrorMessage = "Sport must be 'Running', 'Cycling', or 'Walking'.")]
    public string Sport { get; set; } = string.Empty;
}

/// <summary>
/// Body for PUT /api/categories/{id}
/// </summary>
public class UpdateCategoryDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [RegularExpression("^(Running|Cycling|Walking)$",
        ErrorMessage = "Sport must be 'Running', 'Cycling', or 'Walking'.")]
    public string Sport { get; set; } = string.Empty;
}

// ──────────────────────────────────────────────────────────────────────────────
// ENROLMENT DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Body for POST /api/enrolments — a Participant entering an event
/// </summary>
public class CreateEnrolmentDto
{
    [Required(ErrorMessage = "EventID is required.")]
    public int EventID { get; set; }
}

// ──────────────────────────────────────────────────────────────────────────────
// RESULT DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Body for POST /api/results — Organiser captures a result after the race
/// </summary>
public class CreateResultDto
{
    [Required(ErrorMessage = "EnrolmentID is required.")]
    public int EnrolmentID { get; set; }

    [Required(ErrorMessage = "Finish time is required (e.g. '2:45:30').")]
    [MaxLength(20)]
    public string FinishTime { get; set; } = string.Empty;

    public int? Position { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Body for PUT /api/results/{id} — update an existing result
/// </summary>
public class UpdateResultDto
{
    [Required]
    [MaxLength(20)]
    public string FinishTime { get; set; } = string.Empty;

    public int? Position { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
