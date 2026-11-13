namespace NEXUS.Modules.Documents.Application;

/// <summary>
/// Derives the served MIME type from the stored file name instead of trusting the client-supplied
/// content type. Only documented types are ever returned, and only non-executable types are
/// eligible for inline preview.
/// </summary>
public static class DocumentContentTypes
{
    public const string Fallback = "application/octet-stream";

    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };

    private static readonly HashSet<string> InlinePreviewTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/png",
        "image/jpeg"
    };

    public static string ForFileName(string? fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        return ByExtension.TryGetValue(extension, out var contentType) ? contentType : Fallback;
    }

    public static bool CanPreviewInline(string? contentType)
        => !string.IsNullOrWhiteSpace(contentType) && InlinePreviewTypes.Contains(contentType);
}
