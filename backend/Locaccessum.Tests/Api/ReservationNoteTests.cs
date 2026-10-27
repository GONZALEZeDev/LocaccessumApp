using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Locaccessum.Api.Contracts.Equipment;
using Locaccessum.Api.Contracts.Reservations;
using Locaccessum.Domain.Enums;
using Locaccessum.Tests.Infrastructure;

namespace Locaccessum.Tests.Api;

[Collection("db")]
public class ReservationNoteTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task Create_with_a_note_returns_it_and_it_is_visible_on_the_calendar()
    {
        var (_, ownerToken) = await Register("owner-note1@x.io");
        var invId = await CreateInventory(ownerToken, "NoteTeam1");
        var ownerClient = await AuthedClient(ownerToken);
        var eqRes = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Perceuse", reference = "BOSCH-GSB" });
        var eq = await eqRes.Content.ReadFromJsonAsync<EquipmentResponse>();

        var start = DateTimeOffset.UtcNow.AddDays(5);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { equipmentId = eq!.Id, startsAt = start, endsAt = start.AddHours(2), note = "Risque de prolonger." });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await create.Content.ReadFromJsonAsync<ReservationResponse>();
        body!.Note.Should().Be("Risque de prolonger.");

        var url = $"/api/inventories/{invId}/reservations?from={Uri.EscapeDataString(start.ToString("O"))}&to={Uri.EscapeDataString(start.AddHours(3).ToString("O"))}";
        var calendar = await ownerClient.GetAsync(url);
        calendar.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await calendar.Content.ReadFromJsonAsync<List<ReservationResponse>>();
        items!.Single(i => i.Id == body.Id).Note.Should().Be("Risque de prolonger.");
    }

    [Fact]
    public async Task Create_without_a_note_leaves_it_null()
    {
        var (_, ownerToken) = await Register("owner-note2@x.io");
        var invId = await CreateInventory(ownerToken, "NoteTeam2");
        var ownerClient = await AuthedClient(ownerToken);
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Perceuse", reference = "BOSCH-GSB" });

        var start = DateTimeOffset.UtcNow.AddDays(5);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Perceuse", reference = "BOSCH-GSB", startsAt = start, endsAt = start.AddHours(2) });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await create.Content.ReadFromJsonAsync<ReservationResponse>();
        body!.Note.Should().BeNull();
    }

    [Fact]
    public async Task Create_via_name_and_reference_stack_match_also_carries_the_note()
    {
        var (_, ownerToken) = await Register("owner-note3@x.io");
        var invId = await CreateInventory(ownerToken, "NoteTeam3");
        var ownerClient = await AuthedClient(ownerToken);
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Perceuse", reference = "BOSCH-GSB" });

        var start = DateTimeOffset.UtcNow.AddDays(5);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Perceuse", reference = "BOSCH-GSB", startsAt = start, endsAt = start.AddHours(2), note = "Rapporter chargeur." });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await create.Content.ReadFromJsonAsync<ReservationResponse>();
        body!.Note.Should().Be("Rapporter chargeur.");
    }

    [Fact]
    public async Task Booking_member_can_update_their_own_note()
    {
        var (_, ownerToken) = await Register("owner-note4@x.io");
        var invId = await CreateInventory(ownerToken, "NoteTeam4");
        var ownerClient = await AuthedClient(ownerToken);
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Perceuse", reference = "BOSCH-GSB" });

        var (_, memberToken) = await AddMemberAsync(invId, "member-note4@x.io", MembershipRole.Member);
        var memberClient = await AuthedClient(memberToken);
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var create = await memberClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Perceuse", reference = "BOSCH-GSB", startsAt = start, endsAt = start.AddHours(2) });
        var res = await create.Content.ReadFromJsonAsync<ReservationResponse>();

        var patch = await memberClient.PatchAsJsonAsync($"/api/reservations/{res!.Id}/note", new { note = "Nouvelle note." });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await patch.Content.ReadFromJsonAsync<ReservationResponse>();
        body!.Note.Should().Be("Nouvelle note.");
    }

    [Fact]
    public async Task A_different_member_cannot_update_someone_elses_note()
    {
        var (_, ownerToken) = await Register("owner-note5@x.io");
        var invId = await CreateInventory(ownerToken, "NoteTeam5");
        var ownerClient = await AuthedClient(ownerToken);
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Perceuse", reference = "BOSCH-GSB" });

        var (_, bookerToken) = await AddMemberAsync(invId, "booker-note5@x.io", MembershipRole.Member);
        var bookerClient = await AuthedClient(bookerToken);
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var create = await bookerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Perceuse", reference = "BOSCH-GSB", startsAt = start, endsAt = start.AddHours(2) });
        var res = await create.Content.ReadFromJsonAsync<ReservationResponse>();

        var (_, otherMemberToken) = await AddMemberAsync(invId, "other-note5@x.io", MembershipRole.Member);
        var otherMemberClient = await AuthedClient(otherMemberToken);
        var patch = await otherMemberClient.PatchAsJsonAsync($"/api/reservations/{res!.Id}/note", new { note = "Essai." });
        patch.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_can_update_another_members_note()
    {
        var (_, ownerToken) = await Register("owner-note6@x.io");
        var invId = await CreateInventory(ownerToken, "NoteTeam6");
        var ownerClient = await AuthedClient(ownerToken);
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Perceuse", reference = "BOSCH-GSB" });

        var (_, memberToken) = await AddMemberAsync(invId, "member-note6@x.io", MembershipRole.Member);
        var memberClient = await AuthedClient(memberToken);
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var create = await memberClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Perceuse", reference = "BOSCH-GSB", startsAt = start, endsAt = start.AddHours(2) });
        var res = await create.Content.ReadFromJsonAsync<ReservationResponse>();

        var (_, adminToken) = await AddMemberAsync(invId, "admin-note6@x.io", MembershipRole.Admin);
        var adminClient = await AuthedClient(adminToken);
        var patch = await adminClient.PatchAsJsonAsync($"/api/reservations/{res!.Id}/note", new { note = "Note admin." });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await patch.Content.ReadFromJsonAsync<ReservationResponse>();
        body!.Note.Should().Be("Note admin.");
    }

    [Fact]
    public async Task Updating_the_note_after_cancellation_still_succeeds()
    {
        var (_, ownerToken) = await Register("owner-note7@x.io");
        var invId = await CreateInventory(ownerToken, "NoteTeam7");
        var ownerClient = await AuthedClient(ownerToken);
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Perceuse", reference = "BOSCH-GSB" });

        var start = DateTimeOffset.UtcNow.AddDays(5);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Perceuse", reference = "BOSCH-GSB", startsAt = start, endsAt = start.AddHours(2) });
        var res = await create.Content.ReadFromJsonAsync<ReservationResponse>();

        var cancel = await ownerClient.PostAsync($"/api/reservations/{res!.Id}/cancel", null);
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);

        var patch = await ownerClient.PatchAsJsonAsync($"/api/reservations/{res.Id}/note", new { note = "Apres annulation." });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await patch.Content.ReadFromJsonAsync<ReservationResponse>();
        body!.Note.Should().Be("Apres annulation.");
    }

    [Fact]
    public async Task Setting_the_note_to_null_clears_it()
    {
        var (_, ownerToken) = await Register("owner-note8@x.io");
        var invId = await CreateInventory(ownerToken, "NoteTeam8");
        var ownerClient = await AuthedClient(ownerToken);
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Perceuse", reference = "BOSCH-GSB" });

        var start = DateTimeOffset.UtcNow.AddDays(5);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Perceuse", reference = "BOSCH-GSB", startsAt = start, endsAt = start.AddHours(2), note = "Note initiale." });
        var res = await create.Content.ReadFromJsonAsync<ReservationResponse>();
        res!.Note.Should().Be("Note initiale.");

        var patch = await ownerClient.PatchAsJsonAsync($"/api/reservations/{res.Id}/note", new { note = (string?)null });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await patch.Content.ReadFromJsonAsync<ReservationResponse>();
        body!.Note.Should().BeNull();
    }

    [Fact]
    public async Task Overlong_note_is_400()
    {
        var (_, ownerToken) = await Register("owner-note9@x.io");
        var invId = await CreateInventory(ownerToken, "NoteTeam9");
        var ownerClient = await AuthedClient(ownerToken);
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Perceuse", reference = "BOSCH-GSB" });

        var start = DateTimeOffset.UtcNow.AddDays(5);
        var overlongNote = new string('x', 501);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Perceuse", reference = "BOSCH-GSB", startsAt = start, endsAt = start.AddHours(2), note = overlongNote });
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
