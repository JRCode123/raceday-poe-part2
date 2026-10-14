using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaceDay.API.Models;

/// <summary>
/// Many-to-many bridge: a Participant is enrolled in a specific Event.
/// Matches TABLE 6: Enrolments in the Part 1 SQL script.
/// </summary>
[Table("Enrolments")]
public class Enrolment
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int EnrolmentID { get; set; }

    public int ParticipantID { get; set; }
    public int EventID { get; set; }

    public DateTime EnrolledAt { get; set; } = DateTime.Now;

    /// <summary>
    /// "Confirmed", "Cancelled", or "Waitlisted". Enforced by CHECK in SQL.
    /// Default is "Confirmed" — the assignment requires colour-coded status display.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Confirmed";

    // Navigation properties
    [ForeignKey(nameof(ParticipantID))]
    public Participant? Participant { get; set; }

    [ForeignKey(nameof(EventID))]
    public RaceEvent? Event { get; set; }

    public Result? Result { get; set; }
}
