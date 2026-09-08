using System.ComponentModel.DataAnnotations;

namespace Locaccessum.Api.Contracts.Equipment;

public record CreateEquipmentRequest(
    [Required, StringLength(120, MinimumLength = 1)] string Name,
    [Required, StringLength(120, MinimumLength = 1)] string Reference,
    [StringLength(20000)] string? Informations,
    string? Status);
