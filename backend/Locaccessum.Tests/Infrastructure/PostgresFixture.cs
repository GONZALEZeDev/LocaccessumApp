using Testcontainers.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Locaccessum.Infrastructure.Persistence;

namespace Locaccessum.Tests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    // Testcontainers 4.15 deprecated the parameterless PostgreSqlBuilder() ctor; the
    // image-parameter ctor is the supported replacement. TreatWarningsAsErrors forces this.
    readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var options = new DbContextOptionsBuilder<LocaccessumDbContext>()
            .UseNpgsql(ConnectionString).UseSnakeCaseNamingConvention().Options;
        await using var db = new LocaccessumDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// Builds a fresh <see cref="LocaccessumDbContext"/> bound to the running container.
    /// Callers own the returned instance and must dispose it.
    /// </summary>
    public LocaccessumDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<LocaccessumDbContext>()
            .UseNpgsql(ConnectionString).UseSnakeCaseNamingConvention().Options;
        return new LocaccessumDbContext(options);
    }
}
