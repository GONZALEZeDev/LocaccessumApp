using System.Security.Claims;
using System.Threading.RateLimiting;
using Locaccessum.Api.Abstractions;
using Locaccessum.Api.Authorization;
using Locaccessum.Api.Common;
using Locaccessum.Api.Services;
using Locaccessum.Domain.Enums;
using Locaccessum.Infrastructure;
using Locaccessum.Infrastructure.Auth;
using Locaccessum.Infrastructure.Identity;
using Locaccessum.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ReservationBookingService>();
builder.Services.AddScoped<InventoryRoleHandler>();
builder.Services.AddScoped<IAuthorizationHandler>(sp => sp.GetRequiredService<InventoryRoleHandler>());
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, InternalApiKeyAuthHandler>("Internal", configureOptions: null);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(InventoryPolicies.Member, p => p.Requirements.Add(new InventoryRoleRequirement(MembershipRole.Member)))
    .AddPolicy(InventoryPolicies.Admin, p => p.Requirements.Add(new InventoryRoleRequirement(MembershipRole.Admin)))
    .AddPolicy(InventoryPolicies.Owner, p => p.Requirements.Add(new InventoryRoleRequirement(MembershipRole.Owner)))
    .AddPolicy("InternalKey", p => p.AddAuthenticationSchemes("Internal").RequireClaim(ClaimTypes.Role, "internal"));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration["Cors:FrontOrigin"] ?? "http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));
builder.Services.AddProblemDetails();

// Throttles unauthenticated credential-guessing against login/register: 10 attempts per client per
// minute, shared between the two routes so one can't be used to dodge the other's limit. Keyed by
// remote IP — if this ever sits behind a reverse proxy, that proxy's forwarded-for header needs to
// be wired up (app.UseForwardedHeaders) for the key to reflect the real client instead of the proxy.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.5.20",
            title = "Too Many Requests",
            status = StatusCodes.Status429TooManyRequests,
            detail = "Too many attempts. Please wait before retrying.",
            code = "RATE_LIMITED",
        }, ct);
    };
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LocaccessumDbContext>();
    db.Database.Migrate();

    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var codes = scope.ServiceProvider.GetRequiredService<IUserCodeGenerator>();
    await DbSeeder.SeedAsync(db, hasher, codes);
}

app.UseExceptionHandler();
app.UseSecurityHeaders();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
