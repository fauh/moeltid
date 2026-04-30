using Microsoft.EntityFrameworkCore;
using Moeltid.Models;

namespace Moeltid.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();

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
    }
}
