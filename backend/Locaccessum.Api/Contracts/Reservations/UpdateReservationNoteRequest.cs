using System.ComponentModel.DataAnnotations;

namespace Locaccessum.Api.Contracts.Reservations;

public record UpdateReservationNoteRequest([StringLength(500)] string? Note);
