using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Locaccessum.Domain.Enums;
using Locaccessum.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Locaccessum.Tests.Api;

[Collection("db")]
public class ReservationConcurrencyTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task Parallel_bookings_on_one_stack_never_exceed_unit_count()
    {
        var (_, token) = await Register("race@x.io");
        var invId = await CreateInventory(token, "Race");
        var setupClient = await AuthedClient(token);
        for (var i = 0; i < 3; i++)
            await setupClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment",
                new { name = "Drone", reference = "DJI" });

        var start = DateTimeOffset.UtcNow.AddDays(1);
        var body = new { name = "Drone", reference = "DJI", startsAt = start, endsAt = start.AddHours(2) };

        var attempts = Enumerable.Range(0, 12).Select(async _ =>
        {
            var c = await AuthedClient(token); // each call builds its own HttpClient
            var r = await c.PostAsJsonAsync($"/api/inventories/{invId}/reservations", body);
            return r.StatusCode;
        });
        var results = await Task.WhenAll(attempts);

        results.Count(s => s == HttpStatusCode.Created).Should().Be(3);
        results.Count(s => s == HttpStatusCode.Conflict).Should().Be(9);

        await using var db = Fixture.NewDbContext();
        var confirmed = await db.Reservations.CountAsync(r => r.Status == ReservationStatus.Confirmed);
        confirmed.Should().Be(3);
    }
}
