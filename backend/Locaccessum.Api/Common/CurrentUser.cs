using System.Security.Claims;
using Locaccessum.Api.Abstractions;

namespace Locaccessum.Api.Common;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid Id => Guid.Parse(accessor.HttpContext!.User.FindFirstValue("sub")!);

    public string Email => accessor.HttpContext!.User.FindFirstValue("email")!;
}
