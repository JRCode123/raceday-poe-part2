using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.DTOs;
using RaceDay.API.Filters;
using RaceDay.API.Models;

namespace RaceDay.API.Controllers;

/// <summary>
/// Manages event categories (e.g. Road Running, Charity Walk, Road Cycling).
/// - Public: GET all categories, GET one category
/// - Organiser only: POST, PUT, DELETE
/// </summary>
[ApiController]
[Route("api/categories")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db)
    {
        _db = db;
    }

    private int GetSessionUserId()
        => int.Parse(HttpContext.Session.GetString("UserId")!);

    private async Task<Organiser?> GetCurrentOrganiserAsync()
    {
        var userId = GetSessionUserId();
        return await _db.Organisers.FirstOrDefaultAsync(o => o.UserID == userId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/categories  —  Public
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Get all event categories. Used to populate dropdowns when creating events.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllCategories()
    {
        var categories = await _db.Categories
            .Include(c => c.Organiser).ThenInclude(o => o!.User)
            .Select(c => new
            {
                categoryId = c.CategoryID,
                name = c.Name,
                description = c.Description,
                sport = c.Sport,
                organiser = c.Organiser!.Organisation,
                eventCount = c.Events.Count
            })
            .ToListAsync();

        return Ok(categories);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/categories/{id}  —  Public
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Get one category plus all its events.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategory(int id)
    {
        var category = await _db.Categories
            .Include(c => c.Organiser)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.CategoryID == id);

        if (category == null) return NotFound(new { message = "Category not found." });

        return Ok(new
        {
            categoryId = category.CategoryID,
            name = category.Name,
            description = category.Description,
            sport = category.Sport,
            organiser = category.Organiser?.Organisation,
            events = category.Events.Select(e => new
            {
                e.EventID,
                e.Name,
                eventDate = e.EventDate.ToString("yyyy-MM-dd"),
                e.Location,
                e.IsActive
            })
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/categories  —  Organiser only
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Create a new category. Linked to the logged-in Organiser.</summary>
    [HttpPost]
    [SessionAuthorize(Role = "Organiser")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var organiser = await GetCurrentOrganiserAsync();
        if (organiser == null)
            return StatusCode(500, new { message = "Organiser profile not found." });

        var category = new Category
        {
            OrganiserID = organiser.OrganiserID,
            Name = dto.Name,
            Description = dto.Description,
            Sport = dto.Sport
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return StatusCode(201, new
        {
            categoryId = category.CategoryID,
            name = category.Name,
            sport = category.Sport,
            message = "Category created successfully."
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUT /api/categories/{id}  —  Organiser only (own categories)
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Update a category. Only the Organiser who created it can edit it.</summary>
    [HttpPut("{id:int}")]
    [SessionAuthorize(Role = "Organiser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var organiser = await GetCurrentOrganiserAsync();
        var category = await _db.Categories.FindAsync(id);

        if (category == null) return NotFound(new { message = "Category not found." });
        if (category.OrganiserID != organiser?.OrganiserID)
            return StatusCode(403, new { message = "You can only edit your own categories." });

        category.Name = dto.Name;
        category.Description = dto.Description;
        category.Sport = dto.Sport;

        await _db.SaveChangesAsync();
        return Ok(new { message = "Category updated successfully.", categoryId = category.CategoryID });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DELETE /api/categories/{id}  —  Organiser only (own categories)
    // 409 if events still link to this category
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Delete a category. Fails with 409 if events are still linked to it.</summary>
    [HttpDelete("{id:int}")]
    [SessionAuthorize(Role = "Organiser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var organiser = await GetCurrentOrganiserAsync();
        var category = await _db.Categories
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.CategoryID == id);

        if (category == null) return NotFound(new { message = "Category not found." });
        if (category.OrganiserID != organiser?.OrganiserID)
            return StatusCode(403, new { message = "You can only delete your own categories." });

        // 409 — cannot delete if events are still using this category
        if (category.Events.Any())
            return Conflict(new
            {
                message = $"Cannot delete this category — {category.Events.Count} event(s) are still linked to it. " +
                          "Delete or reassign those events first."
            });

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Category deleted successfully." });
    }
}
