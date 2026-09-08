using System.ComponentModel.DataAnnotations;

namespace Locaccessum.Api.Contracts.Inventories;

public record TransferOwnershipRequest([Required] Guid NewOwnerUserId);
