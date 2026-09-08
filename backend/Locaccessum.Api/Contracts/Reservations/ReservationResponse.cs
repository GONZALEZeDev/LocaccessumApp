namespace Locaccessum.Api.Contracts.Reservations;

public record ReservationResponse(
    Guid Id,
    Guid EquipmentId,
    string EquipmentName,
    string Reference,
    Guid UserId,
    string UserDisplayName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Status);
