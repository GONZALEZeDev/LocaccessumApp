using System.ComponentModel.DataAnnotations;

namespace Locaccessum.Api.Contracts.Inventories;

public record CreateInventoryRequest(
    [Required, StringLength(120, MinimumLength = 1)] string Name,
    [StringLength(2000)] string? Description);
