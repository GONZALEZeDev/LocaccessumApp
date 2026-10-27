using System.ComponentModel.DataAnnotations;

namespace Locaccessum.Api.Contracts.Reservations;

public record CreateReservationRequest(string? Name, string? Reference, Guid? EquipmentId,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    [StringLength(500)] string? Note = null);
