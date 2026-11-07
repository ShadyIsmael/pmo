using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NEXUS.Api.Middleware;

public sealed class ApiErrorResponseFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { StatusCode: >= 400 } result)
        {
            var statusCode = result.StatusCode!.Value;
            context.Result = new ObjectResult(ApiProblemDetails.FromResponse(
                result.Value,
                statusCode,
                context.HttpContext.Request.Path,
                context.HttpContext.TraceIdentifier))
            {
                StatusCode = statusCode,
                ContentTypes = { "application/problem+json" }
            };
        }

        await next();
    }
}
