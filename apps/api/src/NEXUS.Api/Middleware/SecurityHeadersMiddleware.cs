using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace NEXUS.Api.Middleware;

/// <summary>
/// Applies environment-aware security headers to every response.
/// In Development, localhost-origin policies are relaxed to preserve local tooling.
/// In non-Development environments, strict transport security and frame/content
/// restrictions are enforced.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var response = context.Response;

        // Never sniff MIME types.
        response.Headers.Append("X-Content-Type-Options", "nosniff");

        // Prevent clickjacking: deny embedding in frames from any origin.
        response.Headers.Append("X-Frame-Options", "DENY");

        // Limit referrer information sent to external sites.
        response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        // Restrictive CSP for non-Document HTML pages; API JSON responses are unaffected.
        var contentType = response.ContentType ?? string.Empty;
        if (!contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
            && !contentType.StartsWith("application/problem+json", StringComparison.OrdinalIgnoreCase))
        {
            var csp = _environment.IsDevelopment()
                ? "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval' http://localhost:* ws://localhost:*; style-src 'self' 'unsafe-inline' http://localhost:*; img-src 'self' data: http://localhost:*; connect-src 'self' http://localhost:* ws://localhost:*; frame-ancestors 'none'; base-uri 'self';"
                : "default-src 'none'; frame-ancestors 'none'; base-uri 'self';";
            response.Headers.Append("Content-Security-Policy", csp);
        }

        if (!_environment.IsDevelopment())
        {
            // Enforce HTTPS for all future requests in non-Development environments.
            response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        }

        await _next(context);
    }
}
