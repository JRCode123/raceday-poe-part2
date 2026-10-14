using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaceDay.API.Models;

/// <summary>
/// Post-race result captured by an Organiser for a specific enrolment.
/// One result per enrolment (1:1 relationship). 
/// Matches TABLE 7: Results in the Part 1 SQL script.
/// </summary>
[Table("Results")]
public class Result
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ResultID { get; set; }

    /// <summary>
    /// The enrolment this result belongs to. UNIQUE — one result per enrolment.
    /// </summary>
    public int EnrolmentID { get; set; }

    /// <summary>
    /// Stored as text (e.g. "2:45:30") to handle any distance/format.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string FinishTime { get; set; } = string.Empty;

    /// <summary>
    /// Finishing position out of all finishers (e.g. 47).
    /// </summary>
    public int? Position { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.Now;

    // Navigation properties
    [ForeignKey(nameof(EnrolmentID))]
    public Enrolment? Enrolment { get; set; }
}
