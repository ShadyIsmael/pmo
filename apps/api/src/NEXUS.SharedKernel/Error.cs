namespace NEXUS.SharedKernel;

public class Error
{
    public string Code { get; }
    public string Message { get; }
    public int StatusCode { get; }
    public string? FieldName { get; }
    public IReadOnlyDictionary<string, object?>? Details { get; }

    public Error(
        string code,
        string message,
        int statusCode = 400,
        string? fieldName = null,
        IReadOnlyDictionary<string, object?>? details = null)
    {
        Code = code;
        Message = message;
        StatusCode = statusCode;
        FieldName = fieldName;
        Details = details;
    }

    public static Error NotFound(string message = "Resource not found") =>
        new("NOT_FOUND", message, 404);

    public static Error Validation(string message, string? fieldName = null) =>
        new("VALIDATION_ERROR", message, 400, fieldName);

    public static Error BusinessValidation(
        string code,
        string message,
        string? fieldName = null,
        IReadOnlyDictionary<string, object?>? details = null) =>
        new(code, message, 400, fieldName, details);

    public static Error Unauthorized(string message = "Unauthorized") =>
        new("UNAUTHORIZED", message, 401);

    public static Error Forbidden(string message = "Forbidden") =>
        new("FORBIDDEN", message, 403);

    public static Error Conflict(string message) =>
        new("CONFLICT", message, 409);
}
