using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Locaccessum.Api.Contracts.Equipment;
using Locaccessum.Api.Contracts.Reservations;
using Locaccessum.Domain.Entities;
using Locaccessum.Domain.Enums;
using Locaccessum.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Locaccessum.Tests.Api;

[Collection("db")]
public class InternalReservationsTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task Missing_key_is_401()
    {
        var res = await Client.GetAsync("/api/internal/reservations/upcoming");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Wrong_key_is_401()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/internal/reservations/upcoming");
        request.Headers.Add("X-Internal-Api-Key", "wrong-key");
        var res = await Client.SendAsync(request);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Jwt_protected_endpoint_still_works_after_internal_scheme_added()
    {
        var (_, token) = await Register("sanity-check@x.io");
        var client = await AuthedClient(token);
        var res = await client.GetAsync("/api/users/me");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Returns_only_confirmed_unreminded_in_the_23_to_25h_window()
    {
        var (_, ownerToken) = await Register("owner-int1@x.io");
        var invId = await CreateInventory(ownerToken, "IntTeam1");
        var ownerClient = await AuthedClient(ownerToken);
        var eq = await (await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Proj", reference = "P1" })).Content.ReadFromJsonAsync<EquipmentResponse>();

        var now = DateTimeOffset.UtcNow;
        Guid expectedId;
        await using (var db = Fixture.NewDbContext())
        {
            var user = await db.Users.SingleAsync(u => u.Email == "owner-int1@x.io");
            var inWindow = new Reservation { Id = Guid.CreateVersion7(), EquipmentId = eq!.Id, UserId = user.Id, Status = ReservationStatus.Confirmed, StartsAt = now.AddHours(24), EndsAt = now.AddHours(26), CreatedAt = now };
            // NB: StartsAt/EndsAt shifted to [23.5,23.9) to give real margin inside the 23-25h window
            // without overlapping with `inWindow`'s [24,26) slot on the same equipment under the DB's
            // `reservations_no_overlap` exclusion constraint (Task 5-era, EXCLUDE ... WHERE status = 'Confirmed').
            // Tests that this row is filtered by ReminderSentAt, not window boundary.
            var alreadyReminded = new Reservation { Id = Guid.CreateVersion7(), EquipmentId = eq.Id, UserId = user.Id, Status = ReservationStatus.Confirmed, StartsAt = now.AddHours(23.5), EndsAt = now.AddHours(23.9), CreatedAt = now, ReminderSentAt = now };
            var tooFar = new Reservation { Id = Guid.CreateVersion7(), EquipmentId = eq.Id, UserId = user.Id, Status = ReservationStatus.Confirmed, StartsAt = now.AddHours(30), EndsAt = now.AddHours(32), CreatedAt = now };
            var cancelled = new Reservation { Id = Guid.CreateVersion7(), EquipmentId = eq.Id, UserId = user.Id, Status = ReservationStatus.Cancelled, StartsAt = now.AddHours(24), EndsAt = now.AddHours(26), CreatedAt = now, CancelledAt = now };
            db.AddRange(inWindow, alreadyReminded, tooFar, cancelled);
            await db.SaveChangesAsync();
            expectedId = inWindow.Id;
        }

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/internal/reservations/upcoming?windowHours=24");
        request.Headers.Add("X-Internal-Api-Key", "test-internal-key");
        var res = await Client.SendAsync(request);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await res.Content.ReadFromJsonAsync<List<UpcomingReservationResponse>>();
        items!.Should().ContainSingle(i => i.ReservationId == expectedId);
        items.Single().UserEmail.Should().Be("owner-int1@x.io");
        items.Single().EquipmentName.Should().Be("Proj");
    }

    [Fact]
    public async Task Reminder_sent_is_idempotent()
    {
        var (_, ownerToken) = await Register("owner-int2@x.io");
        var invId = await CreateInventory(ownerToken, "IntTeam2");
        var ownerClient = await AuthedClient(ownerToken);
        var eq = await (await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Proj2", reference = "P2" })).Content.ReadFromJsonAsync<EquipmentResponse>();

        var now = DateTimeOffset.UtcNow;
        Guid resId;
        await using (var db = Fixture.NewDbContext())
        {
            var user = await db.Users.SingleAsync(u => u.Email == "owner-int2@x.io");
            var reservation = new Reservation { Id = Guid.CreateVersion7(), EquipmentId = eq!.Id, UserId = user.Id, Status = ReservationStatus.Confirmed, StartsAt = now.AddHours(24), EndsAt = now.AddHours(26), CreatedAt = now };
            db.Add(reservation);
            await db.SaveChangesAsync();
            resId = reservation.Id;
        }

        var req1 = new HttpRequestMessage(HttpMethod.Post, $"/api/internal/reservations/{resId}/reminder-sent");
        req1.Headers.Add("X-Internal-Api-Key", "test-internal-key");
        (await Client.SendAsync(req1)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        DateTimeOffset? firstSentAt;
        await using (var db = Fixture.NewDbContext())
            firstSentAt = (await db.Reservations.SingleAsync(r => r.Id == resId)).ReminderSentAt;
        firstSentAt.Should().NotBeNull();

        var req2 = new HttpRequestMessage(HttpMethod.Post, $"/api/internal/reservations/{resId}/reminder-sent");
        req2.Headers.Add("X-Internal-Api-Key", "test-internal-key");
        (await Client.SendAsync(req2)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var verifyDb = Fixture.NewDbContext();
        (await verifyDb.Reservations.SingleAsync(r => r.Id == resId)).ReminderSentAt.Should().Be(firstSentAt);
    }

    [Fact]
    public async Task Reminder_sent_unknown_id_is_404()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/internal/reservations/{Guid.NewGuid()}/reminder-sent");
        req.Headers.Add("X-Internal-Api-Key", "test-internal-key");
        var res = await Client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
