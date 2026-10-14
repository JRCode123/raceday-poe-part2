using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaceDay.API.Models;

/// <summary>
/// A specific race or event (e.g. Soweto Marathon 2026).
/// Named RaceEvent in C# to avoid conflict with the C# 'event' keyword.
/// Maps to the "Events" table — exactly matching TABLE 5 in the Part 1 SQL script.
/// </summary>
[Table("Events")]
public class RaceEvent
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int EventID { get; set; }

    /// <summary>
    /// The Organiser who created this event.
    /// </summary>
    public int OrganiserID { get; set; }

    /// <summary>
    /// The category this event falls under (e.g. Road Running).
    /// </summary>
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
    public DateOnly EventDate { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    public int MaxParticipants { get; set; } = 500;

    [Column(TypeName = "decimal(10,2)")]
    public decimal EntryFee { get; set; } = 0.00m;

    /// <summary>
    /// False means the event is cancelled or hidden from participants.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    [ForeignKey(nameof(OrganiserID))]
    public Organiser? Organiser { get; set; }

    [ForeignKey(nameof(CategoryID))]
    public Category? Category { get; set; }

    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
