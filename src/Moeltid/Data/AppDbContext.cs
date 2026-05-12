using Microsoft.EntityFrameworkCore;
using Moeltid.Models;

namespace Moeltid.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<MealOption> MealOptions => Set<MealOption>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Invitee> Invitees => Set<Invitee>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<MyEventsAccessToken> MyEventsAccessTokens => Set<MyEventsAccessToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.ManageToken).IsUnique();
            e.Property(x => x.OwnerEmail)
                .HasConversion(v => v.ToLowerInvariant(), v => v);
        });

        modelBuilder.Entity<MealOption>(o =>
        {
            o.HasKey(x => x.Id);
            o.HasIndex(x => x.EventId);
            o.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Attendance>(a =>
        {
            a.HasKey(x => x.Id);
            a.HasIndex(x => x.EditToken).IsUnique();
            a.HasIndex(x => x.EventId);
            a.Property(x => x.Email)
                .HasConversion(
                    v => v == null ? null : v.ToLowerInvariant(),
                    v => v);
            a.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            a.HasOne(x => x.MealOption)
                .WithMany()
                .HasForeignKey(x => x.MealOptionId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        });

        modelBuilder.Entity<Invitee>(i =>
        {
            i.HasKey(x => x.Id);
            i.HasIndex(x => new { x.EventId, x.Email }).IsUnique();
            i.Property(x => x.Email)
                .HasConversion(v => v.ToLowerInvariant(), v => v);
            i.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Reminder>(r =>
        {
            // EventId is both PK and FK — enforces one reminder per event at schema level
            r.HasKey(x => x.EventId);
            r.HasOne(x => x.Event)
                .WithOne()
                .HasForeignKey<Reminder>(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MyEventsAccessToken>(t =>
        {
            t.HasKey(x => x.Token);
            t.HasIndex(x => x.ExpiresAt); // for future janitor cleanup job
            t.Property(x => x.Email)
                .HasConversion(v => v.ToLowerInvariant(), v => v);
        });
    }
}
