using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using NEXUS.SharedKernel;

namespace NEXUS.Api.Middleware;

public static partial class ApiProblemDetails
{
    public static ProblemDetails Create(
        int statusCode,
        string? instance,
        string? traceId,
        string? code = null,
        string? detail = null,
        IReadOnlyDictionary<string, string[]>? errors = null,
        IReadOnlyDictionary<string, object?>? details = null)
    {
        var errorCode = IsStableCode(code) ? code! : CodeForStatus(statusCode);
        var problem = new ProblemDetails
        {
            Type = $"https://errors.nexuspmo.local/{errorCode.ToLowerInvariant().Replace('_', '-')}",
            Title = TitleForStatus(statusCode),
            Status = statusCode,
            Detail = IsSafeClientDetail(detail) ? detail : DetailForStatus(statusCode),
            Instance = instance
        };
        problem.Extensions["code"] = errorCode;
        problem.Extensions["traceId"] = traceId ?? Activity.Current?.Id;
        var safeErrors = SafeErrors(errors, statusCode);
        if (safeErrors is { Count: > 0 })
            problem.Extensions["errors"] = safeErrors;
        var safeDetails = SafeDetails(details);
        if (safeDetails is { Count: > 0 })
            problem.Extensions["details"] = safeDetails;
        return problem;
    }

    public static ProblemDetails FromResponse(object? value, int statusCode, string? instance, string? traceId)
    {
        if (statusCode >= 500)
            return Create(statusCode, instance, traceId);

        if (value is Error error)
        {
            IReadOnlyDictionary<string, string[]>? fieldErrors = null;
            if (!string.IsNullOrWhiteSpace(error.FieldName))
                fieldErrors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [error.FieldName] = [error.Message]
                };
            return Create(
                statusCode,
                instance,
                traceId,
                error.Code,
                error.Message,
                fieldErrors,
                error.Details);
        }

        if (value is ProblemDetails existing)
        {
            existing.Extensions.TryGetValue("errors", out var existingErrors);
            existing.Extensions.TryGetValue("details", out var existingDetails);
            existing.Extensions.TryGetValue("code", out var existingCode);
            var errors = existing is ValidationProblemDetails validation
                ? validation.Errors.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal)
                : ReadErrorMap(existingErrors);
            var details = ReadDetails(existingDetails);
            return Create(
                statusCode,
                instance,
                traceId,
                ReadString(existingCode),
                existing.Detail,
                errors,
                details);
        }

        if (value is null)
            return Create(statusCode, instance, traceId);

        var element = JsonSerializer.SerializeToElement(value, value.GetType());
        var code = ReadString(element, "code", "errorCode");
        var detail = value is string text ? text : ReadString(element, "detail", "message", "error");
        var responseErrors = ReadErrors(element);
        var responseDetails = ReadDetails(element, "details", "parameters");
        var fieldName = ReadString(element, "fieldName", "field");
        if (responseErrors is null && !string.IsNullOrWhiteSpace(fieldName) && !string.IsNullOrWhiteSpace(detail))
        {
            responseErrors = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [fieldName] = [detail]
            };
        }

        return Create(statusCode, instance, traceId, code, detail, responseErrors, responseDetails);
    }

    public static string CodeForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "VALIDATION_ERROR",
        StatusCodes.Status401Unauthorized => "UNAUTHENTICATED",
        StatusCodes.Status403Forbidden => "FORBIDDEN",
        StatusCodes.Status404NotFound => "NOT_FOUND",
        StatusCodes.Status409Conflict => "CONFLICT",
        StatusCodes.Status422UnprocessableEntity => "BUSINESS_VALIDATION_ERROR",
        StatusCodes.Status429TooManyRequests => "RATE_LIMITED",
        _ when statusCode >= 500 => "INTERNAL_ERROR",
        _ => "REQUEST_FAILED"
    };

    private static string TitleForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Validation failed",
        StatusCodes.Status401Unauthorized => "Authentication required",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status422UnprocessableEntity => "Business rule validation failed",
        StatusCodes.Status429TooManyRequests => "Request limit exceeded",
        _ when statusCode >= 500 => "Unexpected server error",
        _ => "Request failed"
    };

    private static string DetailForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest or StatusCodes.Status422UnprocessableEntity =>
            "Review the indicated fields and correct the values before submitting.",
        StatusCodes.Status401Unauthorized => "Your session is not valid. Sign in again to continue.",
        StatusCodes.Status403Forbidden => "You do not have permission to perform this action.",
        StatusCodes.Status404NotFound => "The requested record could not be found. Refresh and try again.",
        StatusCodes.Status409Conflict =>
            "This action conflicts with the latest record. Refresh the information and review it before trying again.",
        StatusCodes.Status429TooManyRequests => "Too many requests were made. Wait a moment and try again.",
        _ when statusCode >= 500 =>
            "An unexpected error occurred. Try again; contact support if the problem continues.",
        _ => "The request could not be completed."
    };

    private static bool IsStableCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && StableCodePattern().IsMatch(code);

    private static bool IsSafeClientDetail(string? detail) =>
        !string.IsNullOrWhiteSpace(detail)
        && !TechnicalDetailPattern().IsMatch(detail)
        && !SensitiveValuePattern().IsMatch(detail);

    private static IReadOnlyDictionary<string, string[]>? SafeErrors(
        IReadOnlyDictionary<string, string[]>? errors,
        int statusCode)
    {
        if (errors is null || errors.Count == 0)
            return null;
        var safe = errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value
                .Select(message => IsSafeClientDetail(message) ? message : DetailForStatus(statusCode))
                .ToArray(),
            StringComparer.Ordinal);
        return safe.Count > 0 ? safe : null;
    }

    private static IReadOnlyDictionary<string, object?>? SafeDetails(
        IReadOnlyDictionary<string, object?>? details)
    {
        if (details is null || details.Count == 0)
            return null;
        var safe = details
            .Where(entry => !SensitiveDetailKeyPattern().IsMatch(entry.Key)
                && IsSafeDetailValue(entry.Value))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        return safe.Count > 0 ? safe : null;
    }

    private static bool IsSafeDetailValue(object? value) => value switch
    {
        null => true,
        string text => !TechnicalDetailPattern().IsMatch(text) && !SensitiveValuePattern().IsMatch(text),
        JsonElement element => IsSafeJsonValue(element),
        bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
            or DateTime or DateTimeOffset or Guid => true,
        _ => false
    };

    private static bool IsSafeJsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => !TechnicalDetailPattern().IsMatch(value.GetString() ?? string.Empty)
            && !SensitiveValuePattern().IsMatch(value.GetString() ?? string.Empty),
        JsonValueKind.Object => value.EnumerateObject().All(property =>
            !SensitiveDetailKeyPattern().IsMatch(property.Name) && IsSafeJsonValue(property.Value)),
        JsonValueKind.Array => value.EnumerateArray().All(IsSafeJsonValue),
        _ => true
    };

    private static string? ReadString(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var property in element.EnumerateObject())
        {
            if (names.Contains(property.Name, StringComparer.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
                return property.Value.GetString();
        }
        return null;
    }

    private static string? ReadString(object? value) =>
        value is JsonElement { ValueKind: JsonValueKind.String } element ? element.GetString() : value as string;

    private static IReadOnlyDictionary<string, string[]>? ReadErrors(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, "errors", StringComparison.OrdinalIgnoreCase))
                return ParseErrorMap(property.Value);
        }
        return null;
    }

    private static IReadOnlyDictionary<string, string[]>? ReadErrorMap(object? value) => value switch
    {
        JsonElement element => ParseErrorMap(element),
        IReadOnlyDictionary<string, string[]> errors => errors,
        IDictionary<string, string[]> errors => new Dictionary<string, string[]>(errors, StringComparer.Ordinal),
        _ => null
    };

    private static IReadOnlyDictionary<string, string[]>? ParseErrorMap(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return null;
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var field in value.EnumerateObject())
        {
            var messages = field.Value.ValueKind switch
            {
                JsonValueKind.Array => field.Value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString()!)
                    .ToArray(),
                JsonValueKind.String when field.Value.GetString() is { } message => [message],
                _ => []
            };
            if (messages.Length > 0)
                errors[field.Name] = messages;
        }
        return errors.Count == 0 ? null : errors;
    }

    private static IReadOnlyDictionary<string, object?>? ReadDetails(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var property in element.EnumerateObject())
        {
            if (names.Contains(property.Name, StringComparer.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.Object)
            {
                return property.Value.EnumerateObject()
                    .ToDictionary(item => item.Name, item => (object?)item.Value.Clone(), StringComparer.Ordinal);
            }
        }
        return null;
    }

    private static IReadOnlyDictionary<string, object?>? ReadDetails(object? value) => value switch
    {
        JsonElement { ValueKind: JsonValueKind.Object } element =>
            element.EnumerateObject().ToDictionary(item => item.Name, item => (object?)item.Value.Clone(), StringComparer.Ordinal),
        IReadOnlyDictionary<string, object?> details => details,
        IDictionary<string, object?> details => new Dictionary<string, object?>(details, StringComparer.Ordinal),
        _ => null
    };

    [GeneratedRegex("^[A-Z][A-Z0-9_]{2,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex StableCodePattern();

    [GeneratedRegex(@"(?:SqlException|SQL(?: Server|STATE| error)|DbUpdateException|DbException|Microsoft\.EntityFrameworkCore|System\.[A-Za-z]+Exception|Invalid (?:object|column) name|Incorrect syntax near|Cannot open database|Data Source\s*=|Initial Catalog\s*=|Server\s*=|The (?:INSERT|UPDATE|DELETE) statement conflicted|Violation of (?:PRIMARY|UNIQUE|FOREIGN|CHECK)|stack trace| at [\w.]+\()", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TechnicalDetailPattern();

    [GeneratedRegex(@"(?:password|secret|token|authorization|credential|connection|sql|query|stack|exception)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveDetailKeyPattern();

    [GeneratedRegex(@"(?:\b(?:password|secret|token|authorization|credential|connection\s*string)\b\s*[:=]\s*\S+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveValuePattern();
}
