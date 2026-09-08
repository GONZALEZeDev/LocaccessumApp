using System.ComponentModel.DataAnnotations;

namespace Locaccessum.Api.Contracts.Inventories;

public record UpdateInventoryRequest([StringLength(120, MinimumLength = 1)] string? Name, [StringLength(2000)] string? Description);
