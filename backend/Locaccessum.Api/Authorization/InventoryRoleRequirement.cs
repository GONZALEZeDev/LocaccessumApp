using Locaccessum.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Locaccessum.Api.Authorization;

public class InventoryRoleRequirement(MembershipRole minimum) : IAuthorizationRequirement
{
    public MembershipRole Minimum { get; } = minimum;
}
