using Locaccessum.Api.Common;
using Locaccessum.Api.Contracts.Auth;
using Locaccessum.Domain.Entities;
using Locaccessum.Infrastructure.Auth;
using Locaccessum.Infrastructure.Identity;
using Locaccessum.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Locaccessum.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    LocaccessumDbContext db,
    IPasswordHasher hasher,
    IUserCodeGenerator codeGen,
    IJwtTokenService jwt) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            return ApiProblem.Conflict("EMAIL_TAKEN", "An account with this email already exists.");
        }

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            PasswordHash = hasher.Hash(req.Password),
            DisplayName = req.DisplayName.Trim(),
            UserCode = await codeGen.NextAsync(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return ApiProblem.Conflict("EMAIL_TAKEN", "An account with this email already exists.");
        }

        var token = jwt.CreateToken(user.Id, user.Email);

        return StatusCode(StatusCodes.Status201Created, new AuthResponse(token, user.Id, user.Email, user.DisplayName, user.UserCode));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();

        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);

        if (user is null || !hasher.Verify(req.Password, user.PasswordHash))
        {
            return ApiProblem.Unauthorized("INVALID_CREDENTIALS", "Invalid email or password.");
        }

        var token = jwt.CreateToken(user.Id, user.Email);

        return Ok(new AuthResponse(token, user.Id, user.Email, user.DisplayName, user.UserCode));
    }
}
