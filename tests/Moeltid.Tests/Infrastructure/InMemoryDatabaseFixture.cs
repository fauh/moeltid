using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moeltid.Data;

namespace Moeltid.Tests.Infrastructure;

// CA1001: disposed via IAsyncLifetime.DisposeAsync — xUnit calls it; analyzer can't see it
#pragma warning disable CA1001
public sealed class InMemoryDatabaseFixture : IAsyncLifetime
#pragma warning restore CA1001
{
    private SqliteConnection? _connection;
    private DbContextOptions<AppDbContext>? _options;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var ctx = new AppDbContext(_options);
        await ctx.Database.MigrateAsync();
    }

    public AppDbContext CreateDbContext()
    {
        ArgumentNullException.ThrowIfNull(_options);
        return new AppDbContext(_options);
    }

    public async Task DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
