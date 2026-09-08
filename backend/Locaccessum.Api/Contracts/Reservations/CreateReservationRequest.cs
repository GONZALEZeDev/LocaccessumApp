namespace Locaccessum.Api.Contracts.Reservations;

public record CreateReservationRequest(string? Name, string? Reference, Guid? EquipmentId, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
