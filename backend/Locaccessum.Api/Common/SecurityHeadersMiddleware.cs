namespace Locaccessum.Api.Common;

/// <summary>
/// Adds the baseline hardening headers found missing during a security review (no response, success
/// or error, carried any of them). The API only ever returns JSON to a known SPA origin — never HTML
/// it renders itself — so the policy below is deliberately the strictest form of each header rather
/// than one tuned for a page with its own scripts/styles/frames.
/// </summary>
public static class SecurityHeadersMiddleware
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            // Ignored by browsers over plain HTTP; takes effect once this sits behind a TLS reverse
            // proxy per the deployment hardening checklist, with nothing extra to turn on here.
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

            await next();
        });
    }
}
