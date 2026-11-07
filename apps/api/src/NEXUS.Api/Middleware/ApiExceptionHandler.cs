using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;

namespace NEXUS.Api.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted)
            return false;

        if (IsExpectedRequestCancellation(exception, httpContext.RequestAborted))
            return true;

        logger.LogError(
            exception,
            "Unhandled API exception for {Method} {Path}; trace {TraceId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.TraceIdentifier);

        // Preserve real failures in logs if the client disconnects while a database
        // error is already in flight, but do not try writing a response to a closed socket.
        if (httpContext.RequestAborted.IsCancellationRequested)
            return true;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(
            ApiProblemDetails.Create(
                StatusCodes.Status500InternalServerError,
                httpContext.Request.Path,
                httpContext.TraceIdentifier),
            contentType: "application/problem+json",
            options: null,
            cancellationToken: cancellationToken);
        return true;
    }

    private static bool IsExpectedRequestCancellation(Exception exception, CancellationToken requestAborted)
    {
        if (!requestAborted.IsCancellationRequested)
            return false;

        if (exception is AggregateException aggregate)
        {
            var failures = aggregate.Flatten().InnerExceptions;
            return failures.Count > 0 && failures.All(failure => IsExpectedRequestCancellation(failure, requestAborted));
        }

        var exceptionChain = new List<Exception>();
        for (var current = exception; current is not null; current = current.InnerException!)
            exceptionChain.Add(current);

        var sqlExceptions = exceptionChain.OfType<SqlException>().ToArray();
        if (sqlExceptions.Any(sqlException => !IsSqlClientCommandCancellation(sqlException)))
            return false;

        return exceptionChain.Any(current => current is OperationCanceledException)
            || sqlExceptions.Length > 0;
    }

    private static bool IsSqlClientCommandCancellation(SqlException exception)
    {
        // Microsoft.Data.SqlClient reports token-canceled commands as two synthetic
        // number 0 / severity 11 / state 0 errors. Match the provider shape rather than
        // localized message text, and only suppress it when RequestAborted is canceled.
        if (exception.Number != 0 || exception.Errors.Count != 2)
            return false;

        foreach (SqlError error in exception.Errors)
        {
            if (error.Number != 0 || error.Class != 11 || error.State != 0)
                return false;
        }

        return true;
    }
}
