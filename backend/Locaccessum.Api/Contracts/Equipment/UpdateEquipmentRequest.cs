using System.ComponentModel.DataAnnotations;

namespace Locaccessum.Api.Contracts.Equipment;

public record UpdateEquipmentRequest(
    [StringLength(120, MinimumLength = 1)] string? Name,
    [StringLength(120, MinimumLength = 1)] string? Reference,
    [StringLength(20000)] string? Informations,
    string? Status);
