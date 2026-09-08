namespace Locaccessum.Api.Contracts.Inventories;

public record InventoryResponse(Guid Id, string Name, string? Description, Guid OwnerId, DateTimeOffset CreatedAt, string MyRole);
