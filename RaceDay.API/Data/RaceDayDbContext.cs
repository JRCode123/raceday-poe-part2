using Microsoft.EntityFrameworkCore;
using RaceDay.API.Models;

namespace RaceDay.API.Data;

/// <summary>
/// Entity Framework Core DbContext for the RaceDay system.
/// Uses Code-First approach — the C# models define the schema.
/// Schema matches the Part 1 SQL script exactly.
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    // ── DbSets (one per table) ────────────────────────────────────────────
    public DbSet<User> Users { get; set; }
    public DbSet<Organiser> Organisers { get; set; }
    public DbSet<Participant> Participants { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<RaceEvent> Events { get; set; }
    public DbSet<Enrolment> Enrolments { get; set; }
    public DbSet<Result> Results { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Users ──────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();  // Email must be unique
            entity.Property(u => u.CreatedAt).HasDefaultValueSql("GETDATE()");
            // Role CHECK constraint: matches the SQL script
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Users_Role", "[Role] IN ('Organiser', 'Participant')"));
        });

        // ── Organisers ─────────────────────────────────────────────────────
        modelBuilder.Entity<Organiser>(entity =>
        {
            entity.HasIndex(o => o.UserID).IsUnique();  // 1:1 with Users
            entity.HasOne(o => o.User)
                  .WithOne(u => u.Organiser)
                  .HasForeignKey<Organiser>(o => o.UserID)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Participants ───────────────────────────────────────────────────
        modelBuilder.Entity<Participant>(entity =>
        {
            entity.HasIndex(p => p.UserID).IsUnique();  // 1:1 with Users
            entity.HasOne(p => p.User)
                  .WithOne(u => u.Participant)
                  .HasForeignKey<Participant>(p => p.UserID)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Participants_Gender", "[Gender] IN ('Male', 'Female', 'Other')"));
        });

        // ── Categories ─────────────────────────────────────────────────────
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasOne(c => c.Organiser)
                  .WithMany(o => o.Categories)
                  .HasForeignKey(c => c.OrganiserID)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Categories_Sport", "[Sport] IN ('Running', 'Cycling', 'Walking')"));
        });

        // ── Events ─────────────────────────────────────────────────────────
        modelBuilder.Entity<RaceEvent>(entity =>
        {
            entity.HasOne(e => e.Organiser)
                  .WithMany(o => o.Events)
                  .HasForeignKey(e => e.OrganiserID)
                  .OnDelete(DeleteBehavior.Restrict);  // No cascade to avoid multiple-path issue
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Events)
                  .HasForeignKey(e => e.CategoryID)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.EntryFee).HasColumnType("decimal(10,2)");
            entity.Property(e => e.MaxParticipants).HasDefaultValue(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        // ── Enrolments ─────────────────────────────────────────────────────
        modelBuilder.Entity<Enrolment>(entity =>
        {
            // UNIQUE: one participant can only enrol in an event once
            entity.HasIndex(en => new { en.ParticipantID, en.EventID }).IsUnique();
            entity.HasOne(en => en.Participant)
                  .WithMany(p => p.Enrolments)
                  .HasForeignKey(en => en.ParticipantID)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(en => en.Event)
                  .WithMany(e => e.Enrolments)
                  .HasForeignKey(en => en.EventID)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.Property(en => en.Status).HasDefaultValue("Confirmed");
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Enrolments_Status", "[Status] IN ('Confirmed', 'Cancelled', 'Waitlisted')"));
        });

        // ── Results ────────────────────────────────────────────────────────
        modelBuilder.Entity<Result>(entity =>
        {
            entity.HasIndex(r => r.EnrolmentID).IsUnique();  // 1:1 with Enrolments
            entity.HasOne(r => r.Enrolment)
                  .WithOne(en => en.Result)
                  .HasForeignKey<Result>(r => r.EnrolmentID)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
