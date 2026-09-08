using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Locaccessum.Tests.Infrastructure;

/// <summary>
/// Async helpers that drive the API's own HTTP surface to set up test fixtures (register a user,
/// create an inventory, ...) instead of poking the database directly.
/// </summary>
/// <remarks>
/// <see cref="RegisterAsync"/> and <see cref="CreateInventoryAsync"/> call routes that do not exist
/// yet (<c>/api/auth/register</c> ships in Task 7, <c>/api/inventories</c> in Task 10). They are
/// written against the documented future contract so the types compile and later tasks can call
/// them; until those routes land, invoking them will fail (404), so no test in this task exercises them.
/// </remarks>
public static class TestDataFactory
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<(Guid userId, string token)> RegisterAsync(
        ApiFactory factory, string email, string password = "Passw0rd!")
    {
        using var client = factory.CreateClient();
        var displayName = $"Test User {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            displayName,
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var token = body.GetProperty("token").GetString()!;
        var userId = body.TryGetProperty("userId", out var userIdProperty)
            ? userIdProperty.GetGuid()
            : body.GetProperty("id").GetGuid();

        return (userId, token);
    }

    public static Task<HttpClient> ClientAsync(ApiFactory factory, string? token = null)
    {
        var client = factory.CreateClient();
        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return Task.FromResult(client);
    }

    public static async Task<Guid> CreateInventoryAsync(ApiFactory factory, string token, string name)
    {
        var client = await ClientAsync(factory, token);
        var response = await client.PostAsJsonAsync("/api/inventories", new { name });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetGuid();
    }
}
