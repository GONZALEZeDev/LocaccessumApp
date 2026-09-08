using Locaccessum.Api.Abstractions;
using Locaccessum.Api.Common;
using Locaccessum.Api.Contracts.Users;
using Locaccessum.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Locaccessum.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(LocaccessumDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await db.Users.FindAsync(currentUser.Id);

        return Ok(new UserResponse(user!.Id, user.Email, user.DisplayName, user.UserCode, user.CreatedAt));
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return ApiProblem.Validation("CODE_REQUIRED", "The code query parameter is required.");

        var normalized = code.Trim().ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.UserCode.ToUpper() == normalized);

        if (user is null)
        {
            return ApiProblem.NotFound("USER_NOT_FOUND", "No user with that code.");
        }

        return Ok(new UserSummaryResponse(user.Id, user.DisplayName, user.UserCode));
    }
}
