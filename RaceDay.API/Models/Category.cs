using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaceDay.API.Models;

/// <summary>
/// Event categories created by Organisers (e.g. Road Running, Charity Walk).
/// Matches TABLE 4: Categories in the Part 1 SQL script.
/// </summary>
[Table("Categories")]
public class Category
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int CategoryID { get; set; }

    /// <summary>
    /// The Organiser who owns this category. On delete cascade from SQL.
    /// </summary>
    public int OrganiserID { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// "Running", "Cycling", or "Walking". Enforced by CHECK in SQL.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Sport { get; set; } = string.Empty;

    // Navigation properties
    [ForeignKey(nameof(OrganiserID))]
    public Organiser? Organiser { get; set; }

    public ICollection<RaceEvent> Events { get; set; } = new List<RaceEvent>();
}
