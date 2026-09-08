using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Locaccessum.Api.Contracts.Inventories;
using Locaccessum.Tests.Infrastructure;

namespace Locaccessum.Tests.Api;

[Collection("db")]
public class InventoriesTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task Create_makes_caller_owner_and_appears_in_list()
    {
        var (_, token) = await Register("owner@x.io");
        var client = await AuthedClient(token);

        var create = await client.PostAsJsonAsync("/api/inventories", new { name = "Atelier", description = "Outils" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var inv = await create.Content.ReadFromJsonAsync<InventoryResponse>();
        inv!.MyRole.Should().Be("Owner");

        var list = await (await client.GetAsync("/api/inventories")).Content.ReadFromJsonAsync<List<InventoryListItemResponse>>();
        list!.Should().ContainSingle(i => i.Id == inv.Id && i.MemberCount == 1);
    }

    [Fact]
    public async Task Get_inventory_as_non_member_is_404()
    {
        var (_, owner) = await Register("o@x.io");
        var id = await CreateInventory(owner, "Priv");
        var (_, stranger) = await Register("s@x.io");
        var strangerClient = await AuthedClient(stranger);
        (await strangerClient.GetAsync($"/api/inventories/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
