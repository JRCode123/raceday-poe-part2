using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using RaceDay.API.Filters;
using RaceDay.Tests.Helpers;
using Xunit;

namespace RaceDay.Tests;

/// <summary>
/// Tests for the SessionAuthorizeAttribute (role-based access control filter).
///
/// These tests verify that:
/// 1. Unauthenticated requests are rejected with 401
/// 2. Wrong-role requests are rejected with 403
/// 3. Correct sessions pass through the filter
///
/// This is the core security requirement of Part 2 — role-based access
/// must be enforced at the API level, not just the UI.
/// </summary>
public class RoleAccessTests
{
    /// <summary>
    /// Creates a fake ActionExecutingContext that the filter can run against.
    /// </summary>
    private static ActionExecutingContext CreateFilterContext(HttpContext httpContext)
    {
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new ActionExecutingContext(
            actionContext,
            filters: new List<IFilterMetadata>(),
            actionArguments: new Dictionary<string, object?>(),
            controller: new object());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UNAUTHENTICATED REQUEST TESTS (no session at all)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SessionAuthorize_NoSession_Returns401Unauthorized()
    {
        // Arrange — empty session (no login)
        var filter = new SessionAuthorizeAttribute();
        var httpContext = TestHelpers.CreateHttpContext(); // no userId in session
        var context = CreateFilterContext(httpContext);

        // Act
        filter.OnActionExecuting(context);

        // Assert — request must be blocked with 401
        var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public void SessionAuthorize_OrganiserRoleRequired_NoSession_Returns401()
    {
        // Arrange
        var filter = new SessionAuthorizeAttribute { Role = "Organiser" };
        var httpContext = TestHelpers.CreateHttpContext(); // not logged in
        var context = CreateFilterContext(httpContext);

        // Act
        filter.OnActionExecuting(context);

        // Assert — 401 because not logged in at all
        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // WRONG ROLE TESTS (logged in but wrong role)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SessionAuthorize_OrganiserRequired_ParticipantSession_Returns403()
    {
        // Arrange — logged in as Participant, but endpoint requires Organiser
        var filter = new SessionAuthorizeAttribute { Role = "Organiser" };
        var httpContext = TestHelpers.CreateHttpContext(userId: "5", role: "Participant");
        var context = CreateFilterContext(httpContext);

        // Act
        filter.OnActionExecuting(context);

        // Assert — 403 Forbidden (authenticated but wrong role)
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public void SessionAuthorize_ParticipantRequired_OrganiserSession_Returns403()
    {
        // Arrange — logged in as Organiser, but endpoint requires Participant
        var filter = new SessionAuthorizeAttribute { Role = "Participant" };
        var httpContext = TestHelpers.CreateHttpContext(userId: "1", role: "Organiser");
        var context = CreateFilterContext(httpContext);

        // Act
        filter.OnActionExecuting(context);

        // Assert — 403 Forbidden
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CORRECT ROLE TESTS (filter should pass through)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SessionAuthorize_OrganiserRequired_OrganiserSession_PassesThrough()
    {
        // Arrange — logged in as Organiser, endpoint requires Organiser
        var filter = new SessionAuthorizeAttribute { Role = "Organiser" };
        var httpContext = TestHelpers.CreateHttpContext(userId: "1", role: "Organiser");
        var context = CreateFilterContext(httpContext);

        // Act
        filter.OnActionExecuting(context);

        // Assert — context.Result is null means the filter did NOT block the request
        Assert.Null(context.Result);
    }

    [Fact]
    public void SessionAuthorize_ParticipantRequired_ParticipantSession_PassesThrough()
    {
        // Arrange
        var filter = new SessionAuthorizeAttribute { Role = "Participant" };
        var httpContext = TestHelpers.CreateHttpContext(userId: "3", role: "Participant");
        var context = CreateFilterContext(httpContext);

        // Act
        filter.OnActionExecuting(context);

        // Assert — filter allows the request through
        Assert.Null(context.Result);
    }

    [Fact]
    public void SessionAuthorize_AnyRoleRequired_OrganiserSession_PassesThrough()
    {
        // Arrange — [SessionAuthorize] with no Role — any logged-in user allowed
        var filter = new SessionAuthorizeAttribute(); // no role restriction
        var httpContext = TestHelpers.CreateHttpContext(userId: "1", role: "Organiser");
        var context = CreateFilterContext(httpContext);

        // Act
        filter.OnActionExecuting(context);

        // Assert — should pass
        Assert.Null(context.Result);
    }

    [Fact]
    public void SessionAuthorize_AnyRoleRequired_ParticipantSession_PassesThrough()
    {
        // Arrange
        var filter = new SessionAuthorizeAttribute();
        var httpContext = TestHelpers.CreateHttpContext(userId: "3", role: "Participant");
        var context = CreateFilterContext(httpContext);

        // Act
        filter.OnActionExecuting(context);

        // Assert
        Assert.Null(context.Result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CROSS-ROLE ISOLATION — critical security tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Organiser_CannotAccess_ParticipantOnlyEndpoint()
    {
        // The enrolment endpoint (POST /api/enrolments) is Participant only.
        // An Organiser must NOT be able to call it.
        var filter = new SessionAuthorizeAttribute { Role = "Participant" };
        var httpContext = TestHelpers.CreateHttpContext(userId: "1", role: "Organiser");
        var context = CreateFilterContext(httpContext);

        filter.OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public void Participant_CannotAccess_OrganiserOnlyEndpoint()
    {
        // The create event endpoint (POST /api/events) is Organiser only.
        // A Participant must NOT be able to call it.
        var filter = new SessionAuthorizeAttribute { Role = "Organiser" };
        var httpContext = TestHelpers.CreateHttpContext(userId: "3", role: "Participant");
        var context = CreateFilterContext(httpContext);

        filter.OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }
}
