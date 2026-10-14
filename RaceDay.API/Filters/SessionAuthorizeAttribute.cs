using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.API.Filters;

/// <summary>
/// Custom action filter that enforces session-based authentication and role checks.
/// 
/// Usage:
///   [SessionAuthorize]              — any logged-in user
///   [SessionAuthorize(Role = "Organiser")]   — Organiser only
///   [SessionAuthorize(Role = "Participant")] — Participant only
///
/// The session stores three keys set at login:
///   "UserId"    — the integer UserID as a string
///   "UserRole"  — "Organiser" or "Participant"
///   "UserName"  — the user's full name
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SessionAuthorizeAttribute : ActionFilterAttribute
{
    /// <summary>
    /// Optional role restriction. If null, any logged-in user is allowed.
    /// </summary>
    public string? Role { get; set; }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetString("UserId");

        // 1. Check that the user is logged in (session has a UserId)
        if (string.IsNullOrEmpty(userId))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                message = "You must be logged in to access this endpoint. POST /api/auth/login first."
            });
            return;
        }

        // 2. If a specific role is required, check it
        if (!string.IsNullOrEmpty(Role))
        {
            var userRole = session.GetString("UserRole");
            if (userRole != Role)
            {
                context.Result = new ObjectResult(new
                {
                    message = $"Access denied. This endpoint requires the '{Role}' role. Your role: '{userRole}'."
                })
                { StatusCode = StatusCodes.Status403Forbidden };
                return;
            }
        }

        base.OnActionExecuting(context);
    }
}
