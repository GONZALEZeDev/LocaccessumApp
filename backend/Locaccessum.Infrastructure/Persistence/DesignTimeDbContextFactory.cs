using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Locaccessum.Infrastructure.Persistence;

/// <summary>
/// Enables <c>dotnet ef</c> to construct the context without a running host.
/// Reads the connection string from the <c>ConnectionStrings__Default</c> environment
/// variable and falls back to a local development default.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LocaccessumDbContext>
{
    public LocaccessumDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=locaccessum;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<LocaccessumDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new LocaccessumDbContext(options);
    }
}
