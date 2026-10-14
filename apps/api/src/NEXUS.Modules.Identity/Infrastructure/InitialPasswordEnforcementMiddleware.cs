using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Infrastructure;

public sealed class InitialPasswordEnforcementMiddleware
{
    private readonly RequestDelegate _next;

    public InitialPasswordEnforcementMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, DbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(context.User.FindFirstValue("tenantId"), out var tenantId))
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "AUTHENTICATION_REQUIRED", "The authenticated account context is not available.");
            return;
        }

        var state = await dbContext.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => user.Id == userId && user.TenantId == tenantId && !user.IsDeleted)
            .Select(user => new
            {
                user.IsActive,
                user.MustChangePassword,
                user.TemporaryPasswordExpiresAt,
                user.PasswordChangedAt
            })
            .FirstOrDefaultAsync(context.RequestAborted);

        if (state is null || !state.IsActive)
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "AUTHENTICATION_REQUIRED", "The account is not available.");
            return;
        }

        var tokenPasswordChangedAt = context.User.FindFirstValue("pwdts");
        var expectedVersion = state.PasswordChangedAt?.Ticks ?? 0;
        var isLegacyUnversionedToken = expectedVersion == 0 && tokenPasswordChangedAt is null;
        if (!isLegacyUnversionedToken
            && (!long.TryParse(tokenPasswordChangedAt, NumberStyles.None, CultureInfo.InvariantCulture, out var tokenVersion)
                || tokenVersion != expectedVersion))
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "CREDENTIALS_INVALIDATED", "Sign in again to continue.");
            return;
        }

        if (state.MustChangePassword && !IsAllowedDuringPasswordChange(context.Request))
        {
            var expired = !state.TemporaryPasswordExpiresAt.HasValue || state.TemporaryPasswordExpiresAt <= DateTime.UtcNow;
            await WriteErrorAsync(
                context,
                StatusCodes.Status403Forbidden,
                expired ? "TEMPORARY_PASSWORD_EXPIRED" : "PASSWORD_CHANGE_REQUIRED",
                expired
                    ? "The temporary password has expired. Contact an administrator to generate a new one."
                    : "Change the temporary password before using NEXUS.");
            return;
        }

        await _next(context);
    }

    private static bool IsAllowedDuringPasswordChange(HttpRequest request)
    {
        var path = request.Path;
        return (HttpMethods.IsGet(request.Method) && string.Equals(path.Value, "/api/auth/me", StringComparison.OrdinalIgnoreCase))
            || (HttpMethods.IsGet(request.Method) && string.Equals(path.Value, "/api/auth/initial-password-policy", StringComparison.OrdinalIgnoreCase))
            || (HttpMethods.IsPost(request.Method)
                && (string.Equals(path.Value, "/api/auth/logout", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(path.Value, "/api/auth/change-initial-password", StringComparison.OrdinalIgnoreCase)));
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status401Unauthorized ? "Unauthorized" : "Password change required",
            Detail = detail
        };
        problem.Extensions["code"] = code;
        await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
    }
}
