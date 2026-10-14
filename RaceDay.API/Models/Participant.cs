using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaceDay.API.Models;

/// <summary>
/// Extended profile for Participant users. Has a 1:1 relationship with Users.
/// Matches TABLE 3: Participants in the Part 1 SQL script.
/// </summary>
[Table("Participants")]
public class Participant
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ParticipantID { get; set; }

    /// <summary>
    /// FK to Users — one User has at most one Participant profile.
    /// </summary>
    public int UserID { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// "Male", "Female", or "Other". Enforced by a CHECK constraint in SQL.
    /// </summary>
    [MaxLength(10)]
    public string? Gender { get; set; }

    [MaxLength(100)]
    public string? EmergencyContact { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserID))]
    public User? User { get; set; }

    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
