using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Locaccessum.Api.Contracts.Reservations;
using Locaccessum.Domain.Enums;
using Locaccessum.Tests.Infrastructure;

namespace Locaccessum.Tests.Api;

[Collection("db")]
public class ReservationCancelTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task Owner_cancels_own_future_booking()
    {
        var (_, ownerToken) = await Register("owner-can1@x.io");
        var invId = await CreateInventory(ownerToken, "CancelTeam1");
        var ownerClient = await AuthedClient(ownerToken);
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Camion", reference = "MB-1" });
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var book = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations", new { name = "Camion", reference = "MB-1", startsAt = start, endsAt = start.AddHours(2) });
        var res = await book.Content.ReadFromJsonAsync<ReservationResponse>();

        var cancel = await ownerClient.PostAsync($"/api/reservations/{res!.Id}/cancel", null);
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await cancel.Content.ReadFromJsonAsync<ReservationResponse>();
        body!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task Plain_member_cannot_cancel_someone_elses_booking()
    {
        var (_, ownerToken) = await Register("owner-can2@x.io");
        var invId = await CreateInventory(ownerToken, "CancelTeam2");
        var ownerClient = await AuthedClient(ownerToken);
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Camion", reference = "MB-2" });
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var book = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations", new { name = "Camion", reference = "MB-2", startsAt = start, endsAt = start.AddHours(2) });
        var res = await book.Content.ReadFromJsonAsync<ReservationResponse>();

        var (_, memberToken) = await AddMemberAsync(invId, "member-can2@x.io", MembershipRole.Member);
        var memberClient = await AuthedClient(memberToken);
        var cancel = await memberClient.PostAsync($"/api/reservations/{res!.Id}/cancel", null);
        cancel.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_cancels_anyones_booking()
    {
        var (_, ownerToken) = await Register("owner-can3@x.io");
        var invId = await CreateInventory(ownerToken, "CancelTeam3");
        var ownerClient = await AuthedClient(ownerToken);
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Camion", reference = "MB-3" });
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var book = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations", new { name = "Camion", reference = "MB-3", startsAt = start, endsAt = start.AddHours(2) });
        var res = await book.Content.ReadFromJsonAsync<ReservationResponse>();

        var (_, adminToken) = await AddMemberAsync(invId, "admin-can3@x.io", MembershipRole.Admin);
        var adminClient = await AuthedClient(adminToken);
        var cancel = await adminClient.PostAsync($"/api/reservations/{res!.Id}/cancel", null);
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Double_cancel_is_409()
    {
        var (_, ownerToken) = await Register("owner-can4@x.io");
        var invId = await CreateInventory(ownerToken, "CancelTeam4");
        var ownerClient = await AuthedClient(ownerToken);
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Camion", reference = "MB-4" });
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var book = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations", new { name = "Camion", reference = "MB-4", startsAt = start, endsAt = start.AddHours(2) });
        var res = await book.Content.ReadFromJsonAsync<ReservationResponse>();

        await ownerClient.PostAsync($"/api/reservations/{res!.Id}/cancel", null);
        var second = await ownerClient.PostAsync($"/api/reservations/{res.Id}/cancel", null);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Rebooking_same_slot_after_cancel_succeeds()
    {
        var (_, ownerToken) = await Register("owner-can5@x.io");
        var invId = await CreateInventory(ownerToken, "CancelTeam5");
        var ownerClient = await AuthedClient(ownerToken);
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Camion", reference = "MB-5" });
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var body = new { name = "Camion", reference = "MB-5", startsAt = start, endsAt = start.AddHours(2) };
        var book = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations", body);
        var res = await book.Content.ReadFromJsonAsync<ReservationResponse>();

        await ownerClient.PostAsync($"/api/reservations/{res!.Id}/cancel", null);

        var rebook = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations", body);
        rebook.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
