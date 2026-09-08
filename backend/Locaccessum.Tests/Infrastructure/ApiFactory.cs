using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Respawn;
using Respawn.Graph;

namespace Locaccessum.Tests.Infrastructure;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> for <c>Program</c> that points the app at the
/// shared <see cref="PostgresFixture"/> container and can reset all app tables between tests via Respawn.
/// </summary>
/// <remarks>
/// <c>Program.cs</c> reads <c>ConnectionStrings:Default</c> eagerly (via <c>AddInfrastructure</c>) on
/// the line right after <c>WebApplication.CreateBuilder(args)</c>, before <c>builder.Build()</c> runs.
/// <see cref="WebApplicationFactory{TEntryPoint}"/> only injects its <see cref="ConfigureWebHost"/>
/// overrides into the captured host builder at the <c>Build()</c> boundary (host-factory-resolver
/// interception), which is too late for that eager read. Environment variables are set in
/// <see cref="InitializeAsync"/> instead, before the host is ever created (i.e. before
/// <c>Program.Main</c> runs via reflection): <c>WebApplication.CreateBuilder</c> wires up
/// <c>AddEnvironmentVariables()</c> synchronously as part of its own construction, so they are visible
/// early enough for the eager read to see them. The <see cref="ConfigureWebHost"/> override below is
/// kept as the normal path for anything that resolves <c>IConfiguration</c> lazily via DI after the
/// host is built.
/// </remarks>
public sealed class ApiFactory(PostgresFixture fixture) : WebApplicationFactory<Program>, IAsyncLifetime
{
    static readonly Dictionary<string, string?> TestSettings = new()
    {
        ["Jwt:Secret"] = "test-only-signing-secret-do-not-use-in-prod!!",
        ["Jwt:Issuer"] = "locaccessum-test",
        ["Jwt:Audience"] = "locaccessum-test",
        ["Jwt:LifetimeHours"] = "8",
        ["InternalApiKey"] = "test-internal-key",
    };

    NpgsqlConnection _resetConnection = null!;
    Respawner _respawner = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>(TestSettings)
            {
                ["ConnectionStrings:Default"] = fixture.ConnectionString,
            };
            config.AddInMemoryCollection(settings);
        });
    }

    public async Task InitializeAsync()
    {
        // Must happen before the first CreateClient()/CreateHost() call — see class remarks.
        // WebApplicationFactory defaults the host environment to "Development" when nothing else
        // sets it, which would make Program.cs's Development-only block (migrate + DbSeeder.SeedAsync,
        // see Task 23) run on every test host startup — reseeding alice/bob/demo data into a table set
        // that ResetAsync() just truncated, and breaking tests that assert on global (cross-inventory)
        // row counts. Force a non-Development environment so that block stays a local/`dotnet run` only
        // concern; PostgresFixture.InitializeAsync() already migrates the schema independently for tests.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", fixture.ConnectionString);
        Environment.SetEnvironmentVariable("Jwt__Secret", TestSettings["Jwt:Secret"]);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestSettings["Jwt:Issuer"]);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestSettings["Jwt:Audience"]);
        Environment.SetEnvironmentVariable("Jwt__LifetimeHours", TestSettings["Jwt:LifetimeHours"]);
        Environment.SetEnvironmentVariable("InternalApiKey", TestSettings["InternalApiKey"]);

        _resetConnection = new NpgsqlConnection(fixture.ConnectionString);
        await _resetConnection.OpenAsync();
        _respawner = await Respawner.CreateAsync(_resetConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Table("__EFMigrationsHistory")],
        });
    }

    /// <summary>Deletes all rows from every app table (schema untouched) so tests start from a clean slate.</summary>
    public Task ResetAsync() => _respawner.ResetAsync(_resetConnection);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _resetConnection.DisposeAsync();
        await base.DisposeAsync();
    }
}
