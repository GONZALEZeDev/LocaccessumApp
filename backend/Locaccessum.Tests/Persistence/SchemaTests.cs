using FluentAssertions;
using Locaccessum.Tests.Infrastructure;

namespace Locaccessum.Tests.Persistence;

[Collection("db")]
public class SchemaTests(PostgresFixture fx)
{
    [Fact]
    public async Task Exclusion_constraint_exists()
    {
        await using var conn = new Npgsql.NpgsqlConnection(fx.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand(
            "select 1 from pg_constraint where conname = 'reservations_no_overlap'", conn);
        var found = await cmd.ExecuteScalarAsync();
        found.Should().NotBeNull();
    }
}
