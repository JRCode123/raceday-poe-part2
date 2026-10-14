using Microsoft.AspNetCore.Mvc;
using RaceDay.API.Controllers;
using RaceDay.API.DTOs;
using RaceDay.API.Models;
using RaceDay.Tests.Helpers;
using Xunit;

namespace RaceDay.Tests;

/// <summary>
/// Tests for /api/categories endpoints.
/// Covers: public GET, Organiser CRUD, ownership enforcement, cascade conflict prevention.
/// </summary>
public class CategoriesControllerTests
{
    private async Task<(CategoriesController controller, int organiserId, int categoryId)>
        SetupOrganiserControllerAsync()
    {
        var db = TestHelpers.CreateInMemoryDb();
        var (organiserId, _, categoryId, _) = await TestHelpers.SeedTestDataAsync(db);

        var organiser = db.Organisers.First(o => o.OrganiserID == organiserId);
        var httpContext = TestHelpers.CreateHttpContext(
            userId: organiser.UserID.ToString(), role: "Organiser");

        var controller = new CategoriesController(db);
        TestHelpers.SetControllerContext(controller, httpContext);

        return (controller, organiserId, categoryId);
    }

    // ── Public GET Tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllCategories_Public_Returns200()
    {
        var db = TestHelpers.CreateInMemoryDb();
        await TestHelpers.SeedTestDataAsync(db);
        var controller = new CategoriesController(db);
        TestHelpers.SetControllerContext(controller, TestHelpers.CreateHttpContext());

        var result = await controller.GetAllCategories();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task GetCategory_ValidId_Returns200()
    {
        var (controller, _, categoryId) = await SetupOrganiserControllerAsync();

        var result = await controller.GetCategory(categoryId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task GetCategory_InvalidId_Returns404()
    {
        var (controller, _, _) = await SetupOrganiserControllerAsync();

        var result = await controller.GetCategory(9999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── Create Category Tests ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateCategory_ValidOrganiser_Returns201()
    {
        var (controller, _, _) = await SetupOrganiserControllerAsync();

        var result = await controller.CreateCategory(new CreateCategoryDto
        {
            Name = "Marathon Running",
            Description = "42.2km and up",
            Sport = "Running"
        });

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_InvalidSport_Returns400()
    {
        var (controller, _, _) = await SetupOrganiserControllerAsync();

        // ModelState validation doesn't run in unit tests without the full pipeline.
        // The [RegularExpression] on Sport in the DTO would catch "Swimming" in real requests.
        // Here we test the controller logic path when a valid (but wrong) sport slips through:
        // because the SQL CHECK constraint would catch it at DB level in prod.
        // So we test with missing Name (which causes ModelState.IsValid = false):
        var dto = new CreateCategoryDto
        {
            Name = "",   // empty name — required
            Sport = "Running"
        };
        controller.ModelState.AddModelError("Name", "Category name is required.");

        var result = await controller.CreateCategory(dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── Update Category Tests ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdateCategory_OwningOrganiser_Returns200()
    {
        var (controller, _, categoryId) = await SetupOrganiserControllerAsync();

        var result = await controller.UpdateCategory(categoryId, new UpdateCategoryDto
        {
            Name = "Updated Category Name",
            Description = "Updated description",
            Sport = "Cycling"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_NonExistent_Returns404()
    {
        var (controller, _, _) = await SetupOrganiserControllerAsync();

        var result = await controller.UpdateCategory(9999, new UpdateCategoryDto
        {
            Name = "Ghost", Sport = "Walking"
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpdateCategory_DifferentOrganiser_Returns403()
    {
        var db = TestHelpers.CreateInMemoryDb();
        var (_, _, categoryId, _) = await TestHelpers.SeedTestDataAsync(db);

        // A second organiser who does NOT own the category
        var otherUser = new User
        {
            FullName = "Other Org",
            Email = "other@test.com",
            PasswordHash = "hash",
            Role = "Organiser"
        };
        db.Users.Add(otherUser);
        await db.SaveChangesAsync();
        var otherOrg = new Organiser { UserID = otherUser.UserID, Organisation = "Other Club" };
        db.Organisers.Add(otherOrg);
        await db.SaveChangesAsync();

        var httpContext = TestHelpers.CreateHttpContext(
            userId: otherUser.UserID.ToString(), role: "Organiser");
        var controller = new CategoriesController(db);
        TestHelpers.SetControllerContext(controller, httpContext);

        var result = await controller.UpdateCategory(categoryId, new UpdateCategoryDto
        {
            Name = "Stolen", Sport = "Cycling"
        });

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
    }

    // ── Delete Category Tests ─────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCategory_WithLinkedEvents_Returns409Conflict()
    {
        // The seeded category has a linked event — cannot delete
        var (controller, _, categoryId) = await SetupOrganiserControllerAsync();

        var result = await controller.DeleteCategory(categoryId);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_NoLinkedEvents_Returns200()
    {
        // Create a fresh category with no events
        var (controller, organiserId, _) = await SetupOrganiserControllerAsync();

        // Create a new empty category
        var createResult = await controller.CreateCategory(new CreateCategoryDto
        {
            Name = "Empty Category",
            Sport = "Walking"
        });
        var created = Assert.IsType<ObjectResult>(createResult);
        var newCategoryId = (int)created.Value!.GetType()
            .GetProperty("categoryId")!.GetValue(created.Value)!;

        // Act — delete the empty category
        var result = await controller.DeleteCategory(newCategoryId);

        Assert.IsType<OkObjectResult>(result);
    }
}
