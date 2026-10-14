// ============================================================
// RaceDay API — Part 2
// Student: Junior | IIE Rosebank College
// ASP.NET Core Web API with Entity Framework Core & Sessions
// ============================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.API.Data;

var builder = WebApplication.CreateBuilder(args);

// ── Database: Entity Framework Core (Code-First, SQL Server) ──────────────
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Session: server-side session stores UserId and Role ───────────────────
// The session cookie is sent with every request once the user logs in.
// Non-public endpoints read the session to confirm identity and role.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);   // session lasts 2 hours
    options.Cookie.HttpOnly = true;                 // JS cannot read the cookie
    options.Cookie.IsEssential = true;              // GDPR: needed for the app to work
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// ── MVC Controllers ───────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── Swagger / OpenAPI ─────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "RESTful API for the RaceDay South African road running, walking and cycling event management system. " +
                      "Supports two roles — Organiser and Participant. " +
                      "To test protected endpoints: POST /api/auth/login first, then the session cookie is set automatically.",
        Contact = new OpenApiContact { Name = "Junior", Email = "st10469891@rcconnect.edu.za" }
    });
});

// ── CORS (allow any origin in development) ────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// ─────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Development middleware ─────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
        c.RoutePrefix = string.Empty; // Swagger at root URL
    });
}

app.UseCors("AllowAll");
app.UseSession();          // Session MUST come before MapControllers
app.UseAuthorization();
app.MapControllers();

app.Run();

// Make Program accessible to the test project
public partial class Program { }
