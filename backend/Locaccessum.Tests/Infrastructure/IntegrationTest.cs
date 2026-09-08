using Locaccessum.Domain.Entities;
using Locaccessum.Domain.Enums;

namespace Locaccessum.Tests.Infrastructure;

/// <summary>
/// Base class for API integration tests: owns an <see cref="ApiFactory"/> bound to the shared
/// <see cref="PostgresFixture"/>, resets the database before every test, and exposes a plain
/// <see cref="Client"/> plus small helpers built on <see cref="TestDataFactory"/> for tests that
/// need an authenticated user or seeded data.
/// </summary>
/// <remarks>
/// Concrete subclasses must carry <c>[Collection("db")]</c> themselves (xUnit does not honor a
/// collection attribute placed on an abstract base class) and pass the shared
/// <see cref="PostgresFixture"/> through their own primary constructor, e.g.
/// <c>public class FooTests(PostgresFixture fx) : IntegrationTest(fx)</c>.
/// </remarks>
public abstract class IntegrationTest(PostgresFixture fixture) : IAsyncLifetime
{
    protected readonly ApiFactory Factory = new(fixture);

    /// <summary>
    /// Exposes the shared <see cref="PostgresFixture"/> to subclasses so tests can open their own
    /// fresh <see cref="Locaccessum.Infrastructure.Persistence.LocaccessumDbContext"/> (via
    /// <see cref="PostgresFixture.NewDbContext"/>) to seed or assert on rows for entities that have
    /// no API of their own yet (e.g. Equipment/Reservation), the same way <see cref="AddMemberAsync"/>
    /// seeds Memberships directly.
    /// </summary>
    protected PostgresFixture Fixture => fixture;

    protected HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Factory.InitializeAsync();
        await Factory.ResetAsync();
        Client = Factory.CreateClient();
    }

    public Task DisposeAsync() => ((IAsyncLifetime)Factory).DisposeAsync();

    /// <summary>Registers a new user via the real HTTP endpoint and returns its id and bearer token.</summary>
    protected Task<(Guid userId, string token)> Register(string email, string password = "Passw0rd!")
        => TestDataFactory.RegisterAsync(Factory, email, password);

    /// <summary>Builds an <see cref="HttpClient"/> carrying the given bearer token (or anonymous if <c>null</c>).</summary>
    protected Task<HttpClient> AuthedClient(string? token = null)
        => TestDataFactory.ClientAsync(Factory, token);

    /// <summary>Creates an inventory via the real HTTP endpoint, authenticated as the given token, and returns its id.</summary>
    protected Task<Guid> CreateInventory(string token, string name)
        => TestDataFactory.CreateInventoryAsync(Factory, token, name);

    /// <summary>
    /// Registers a new user and seeds a <see cref="Membership"/> for it directly via the database.
    /// There is no API path to add a second member to an inventory yet (invitations land in later
    /// tasks), so tests that need a non-owner Admin/Member must seed that membership this way.
    /// </summary>
    protected async Task<(Guid userId, string token)> AddMemberAsync(Guid inventoryId, string email, MembershipRole role)
    {
        var (userId, token) = await Register(email);
        await using var db = fixture.NewDbContext();
        db.Add(new Membership
        {
            Id = Guid.CreateVersion7(),
            InventoryId = inventoryId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return (userId, token);
    }
}
