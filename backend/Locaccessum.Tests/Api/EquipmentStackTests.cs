using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Locaccessum.Api.Contracts.Equipment;
using Locaccessum.Tests.Infrastructure;
using Xunit;

namespace Locaccessum.Tests.Api;

[Collection("db")]
public class EquipmentStackTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task Grouped_collapses_same_name_and_reference()
    {
        var (_, ownerToken) = await Register("owner-stack1@x.io");
        var invId = await CreateInventory(ownerToken, "StackTeam");
        var ownerClient = await AuthedClient(ownerToken);
        for (var i = 0; i < 3; i++)
            await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Frigo 19T", reference = "RENAULT-D" });
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Frigo 19T", reference = "VOLVO-FH" });

        var res = await ownerClient.GetAsync($"/api/inventories/{invId}/equipment?grouped=true");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var stacks = await res.Content.ReadFromJsonAsync<List<EquipmentStackResponse>>();
        stacks!.Should().HaveCount(2);
        var renault = stacks.Single(s => s.Reference == "RENAULT-D");
        renault.UnitsTotal.Should().Be(3);
        renault.UnitIds.Should().HaveCount(3);
    }

    [Fact]
    public async Task Ungrouped_still_returns_flat_list()
    {
        var (_, ownerToken) = await Register("owner-stack2@x.io");
        var invId = await CreateInventory(ownerToken, "StackTeam2");
        var ownerClient = await AuthedClient(ownerToken);
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Perceuse", reference = "BOSCH-1" });
        await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Perceuse", reference = "BOSCH-1" });

        var res = await ownerClient.GetAsync($"/api/inventories/{invId}/equipment");
        var items = await res.Content.ReadFromJsonAsync<List<EquipmentResponse>>();
        items!.Should().HaveCount(2);
    }
}
