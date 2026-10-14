using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaceDay.API.Models;

/// <summary>
/// Extended profile for Organiser users. Has a 1:1 relationship with Users.
/// Matches TABLE 2: Organisers in the Part 1 SQL script.
/// </summary>
[Table("Organisers")]
public class Organiser
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int OrganiserID { get; set; }

    /// <summary>
    /// FK to Users — one User has at most one Organiser profile.
    /// </summary>
    public int UserID { get; set; }

    [Required]
    [MaxLength(150)]
    public string Organisation { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? ContactPhone { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserID))]
    public User? User { get; set; }

    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<RaceEvent> Events { get; set; } = new List<RaceEvent>();
}
