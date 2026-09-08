namespace Locaccessum.Api.Contracts.Reservations;

public record UpcomingReservationResponse(
    Guid ReservationId,
    string UserEmail,
    string UserDisplayName,
    string EquipmentName,
    string Reference,
    string InventoryName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);
