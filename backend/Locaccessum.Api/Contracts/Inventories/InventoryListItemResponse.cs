namespace Locaccessum.Api.Contracts.Inventories;

public record InventoryListItemResponse(Guid Id, string Name, string? Description, string MyRole, int MemberCount);
