using System.ComponentModel.DataAnnotations;

namespace Locaccessum.Api.Contracts.Members;

public record UpdateMemberRoleRequest([Required] string Role);
