namespace Locaccessum.Api.Contracts.Equipment;

public record EquipmentResponse(
    Guid Id,
    Guid InventoryId,
    string Name,
    string Reference,
    string? Informations,
    string Status,
    DateTimeOffset CreatedAt);
