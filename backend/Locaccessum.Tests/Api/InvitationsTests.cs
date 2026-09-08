using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Locaccessum.Api.Contracts.Inventories;
using Locaccessum.Api.Contracts.Invitations;
using Locaccessum.Api.Contracts.Users;
using Locaccessum.Domain.Enums;
using Locaccessum.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Locaccessum.Tests.Api;

[Collection("db")]
public class InvitationsTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task Create_invitation_happy_path_is_pending()
    {
        var (_, ownerToken) = await Register("owner1@x.io");
        var invId = await CreateInventory(ownerToken, "Team");
        var (_, inviteeToken) = await Register("invitee1@x.io");
        var inviteeClient = await AuthedClient(inviteeToken);
        var me = await (await inviteeClient.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<UserResponse>();
        var ownerClient = await AuthedClient(ownerToken);

        var res = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/invitations", new { userCode = me!.UserCode, role = "Member" });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<InvitationResponse>();
        body!.Status.Should().Be("Pending");
        body.Role.Should().Be("Member");
        body.InvitedUserId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Duplicate_pending_invitation_is_409()
    {
        var (_, ownerToken) = await Register("owner2@x.io");
        var invId = await CreateInventory(ownerToken, "Team2");
        var (_, inviteeToken) = await Register("invitee2@x.io");
        var inviteeClient = await AuthedClient(inviteeToken);
        var me = await (await inviteeClient.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<UserResponse>();
        var ownerClient = await AuthedClient(ownerToken);

        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/invitations", new { userCode = me!.UserCode, role = "Member" });
        var second = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/invitations", new { userCode = me.UserCode, role = "Member" });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be("INVITATION_PENDING");
    }

    [Fact]
    public async Task Inviting_existing_member_is_409()
    {
        var (_, ownerToken) = await Register("owner3@x.io");
        var invId = await CreateInventory(ownerToken, "Team3");
        var (_, memberToken) = await AddMemberAsync(invId, "member3@x.io", MembershipRole.Member);
        var memberClient = await AuthedClient(memberToken);
        var me = await (await memberClient.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<UserResponse>();
        var ownerClient = await AuthedClient(ownerToken);

        var res = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/invitations", new { userCode = me!.UserCode, role = "Admin" });
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be("ALREADY_MEMBER");
    }

    [Fact]
    public async Task Unknown_user_code_is_404()
    {
        var (_, ownerToken) = await Register("owner5@x.io");
        var invId = await CreateInventory(ownerToken, "Team5");
        var ownerClient = await AuthedClient(ownerToken);

        var res = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/invitations", new { userCode = "LOCA-ZZZZZZ", role = "Member" });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Inviting_as_owner_role_is_400()
    {
        var (_, ownerToken) = await Register("owner6@x.io");
        var invId = await CreateInventory(ownerToken, "Team6");
        var (_, inviteeToken) = await Register("invitee6@x.io");
        var inviteeClient = await AuthedClient(inviteeToken);
        var me = await (await inviteeClient.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<UserResponse>();
        var ownerClient = await AuthedClient(ownerToken);

        var res = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/invitations", new { userCode = me!.UserCode, role = "Owner" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Invitee_sees_pending_invitation_in_mine_list()
    {
        var (_, ownerToken) = await Register("owner4@x.io");
        var invId = await CreateInventory(ownerToken, "Team4");
        var (_, inviteeToken) = await Register("invitee4@x.io");
        var inviteeClient = await AuthedClient(inviteeToken);
        var me = await (await inviteeClient.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<UserResponse>();
        var ownerClient = await AuthedClient(ownerToken);
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/invitations", new { userCode = me!.UserCode, role = "Member" });

        var mine = await (await inviteeClient.GetAsync("/api/invitations")).Content.ReadFromJsonAsync<List<InvitationResponse>>();
        mine!.Should().ContainSingle(i => i.InventoryId == invId && i.Status == "Pending");
    }

    [Fact]
    public async Task Admin_lists_all_invitations_for_inventory()
    {
        var (_, ownerToken) = await Register("owner7@x.io");
        var invId = await CreateInventory(ownerToken, "Team7");
        var (_, invitee1Token) = await Register("invitee7a@x.io");
        var me1 = await (await (await AuthedClient(invitee1Token)).GetAsync("/api/users/me")).Content.ReadFromJsonAsync<UserResponse>();
        var ownerClient = await AuthedClient(ownerToken);
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/invitations", new { userCode = me1!.UserCode, role = "Member" });

        var list = await (await ownerClient.GetAsync($"/api/inventories/{invId}/invitations")).Content.ReadFromJsonAsync<List<InvitationResponse>>();
        list!.Should().ContainSingle(i => i.InvitedUserId == me1.UserId);
    }

    [Fact]
    public async Task Accept_creates_membership_with_invited_role()
    {
        var (_, ownerToken) = await Register("owner-acc1@x.io");
        var invId = await CreateInventory(ownerToken, "AcceptTeam");
        var (_, inviteeToken) = await Register("invitee-acc1@x.io");
        var inviteeClient = await AuthedClient(inviteeToken);
        var me = await (await inviteeClient.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<UserResponse>();
        var ownerClient = await AuthedClient(ownerToken);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/invitations", new { userCode = me!.UserCode, role = "Admin" });
        var invitation = await create.Content.ReadFromJsonAsync<InvitationResponse>();

        var accept = await inviteeClient.PostAsync($"/api/invitations/{invitation!.Id}/accept", null);
        accept.StatusCode.Should().Be(HttpStatusCode.OK);

        var inventories = await (await inviteeClient.GetAsync("/api/inventories")).Content.ReadFromJsonAsync<List<InventoryListItemResponse>>();
        inventories!.Should().ContainSingle(i => i.Id == invId && i.MyRole == "Admin");
    }

    [Fact]
    public async Task Accept_twice_is_409_and_single_membership()
    {
        var (_, ownerToken) = await Register("owner-acc2@x.io");
        var invId = await CreateInventory(ownerToken, "AcceptTwiceTeam");
        var (inviteeId, inviteeToken) = await Register("invitee-acc2@x.io");
        var inviteeClient = await AuthedClient(inviteeToken);
        var me = await (await inviteeClient.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<UserResponse>();
        var ownerClient = await AuthedClient(ownerToken);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/invitations", new { userCode = me!.UserCode, role = "Member" });
        var invitation = await create.Content.ReadFromJsonAsync<InvitationResponse>();

        var first = await inviteeClient.PostAsync($"/api/invitations/{invitation!.Id}/accept", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await inviteeClient.PostAsync($"/api/invitations/{invitation.Id}/accept", null);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await using var db = Fixture.NewDbContext();
        var count = await db.Memberships.CountAsync(m => m.InventoryId == invId && m.UserId == inviteeId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task Decline_sets_status_and_no_membership()
    {
        var (_, ownerToken) = await Register("owner-dec1@x.io");
        var invId = await CreateInventory(ownerToken, "DeclineTeam");
        var (inviteeId, inviteeToken) = await Register("invitee-dec1@x.io");
        var inviteeClient = await AuthedClient(inviteeToken);
        var me = await (await inviteeClient.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<UserResponse>();
        var ownerClient = await AuthedClient(ownerToken);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/invitations", new { userCode = me!.UserCode, role = "Member" });
        var invitation = await create.Content.ReadFromJsonAsync<InvitationResponse>();

        var decline = await inviteeClient.PostAsync($"/api/invitations/{invitation!.Id}/decline", null);
        decline.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = Fixture.NewDbContext();
        var stored = await db.Invitations.SingleAsync(i => i.Id == invitation.Id);
        stored.Status.Should().Be(InvitationStatus.Declined);
        stored.RespondedAt.Should().NotBeNull();
        (await db.Memberships.AnyAsync(m => m.InventoryId == invId && m.UserId == inviteeId)).Should().BeFalse();
    }

    [Fact]
    public async Task Revoke_by_admin_blocks_accept()
    {
        var (_, ownerToken) = await Register("owner-rev1@x.io");
        var invId = await CreateInventory(ownerToken, "RevokeTeam");
        var (_, inviteeToken) = await Register("invitee-rev1@x.io");
        var inviteeClient = await AuthedClient(inviteeToken);
        var me = await (await inviteeClient.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<UserResponse>();
        var ownerClient = await AuthedClient(ownerToken);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/invitations", new { userCode = me!.UserCode, role = "Member" });
        var invitation = await create.Content.ReadFromJsonAsync<InvitationResponse>();

        var revoke = await ownerClient.DeleteAsync($"/api/invitations/{invitation!.Id}");
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var accept = await inviteeClient.PostAsync($"/api/invitations/{invitation.Id}/accept", null);
        accept.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Third_party_cannot_accept()
    {
        var (_, ownerToken) = await Register("owner-tp1@x.io");
        var invId = await CreateInventory(ownerToken, "ThirdPartyTeam");
        var (_, inviteeToken) = await Register("invitee-tp1@x.io");
        var inviteeClient = await AuthedClient(inviteeToken);
        var me = await (await inviteeClient.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<UserResponse>();
        var ownerClient = await AuthedClient(ownerToken);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/invitations", new { userCode = me!.UserCode, role = "Member" });
        var invitation = await create.Content.ReadFromJsonAsync<InvitationResponse>();

        var (_, strangerToken) = await Register("stranger-tp1@x.io");
        var strangerClient = await AuthedClient(strangerToken);
        var accept = await strangerClient.PostAsync($"/api/invitations/{invitation!.Id}/accept", null);
        accept.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Non_member_cannot_list_invitations()
    {
        var (_, ownerToken) = await Register("owner-nm1@x.io");
        var invId = await CreateInventory(ownerToken, "NonMemberTeam");
        var (_, strangerToken) = await Register("stranger-nm1@x.io");
        var strangerClient = await AuthedClient(strangerToken);

        var res = await strangerClient.GetAsync($"/api/inventories/{invId}/invitations");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Non_admin_cannot_revoke()
    {
        var (_, ownerToken) = await Register("owner-nr1@x.io");
        var invId = await CreateInventory(ownerToken, "NonAdminRevokeTeam");
        var (_, memberToken) = await AddMemberAsync(invId, "member-nr1@x.io", MembershipRole.Member);
        var (_, inviteeToken) = await Register("invitee-nr1@x.io");
        var inviteeClient = await AuthedClient(inviteeToken);
        var me = await (await inviteeClient.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<UserResponse>();
        var ownerClient = await AuthedClient(ownerToken);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/invitations", new { userCode = me!.UserCode, role = "Member" });
        var invitation = await create.Content.ReadFromJsonAsync<InvitationResponse>();

        var memberClient = await AuthedClient(memberToken);
        var revoke = await memberClient.DeleteAsync($"/api/invitations/{invitation!.Id}");
        revoke.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
