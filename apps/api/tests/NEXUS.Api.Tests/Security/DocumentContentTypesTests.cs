using NEXUS.Modules.Documents.Application;
using Xunit;

namespace NEXUS.Api.Tests.Security;

/// <summary>
/// Regression coverage for document content-type handling: the served type is derived from the
/// stored file name, never from the client-supplied upload header, and only non-executable types
/// may be previewed inline.
/// </summary>
public class DocumentContentTypesTests
{
    [Theory]
    [InlineData("report.pdf", "application/pdf")]
    [InlineData("photo.PNG", "image/png")]
    [InlineData("photo.jpeg", "image/jpeg")]
    [InlineData("notes.txt", "text/plain; charset=utf-8")]
    [InlineData("data.csv", "text/csv; charset=utf-8")]
    [InlineData("plan.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public void ForFileName_maps_allowed_extensions_to_fixed_types(string fileName, string expected)
    {
        Assert.Equal(expected, DocumentContentTypes.ForFileName(fileName));
    }

    [Theory]
    [InlineData("payload.html")]
    [InlineData("payload.svg")]
    [InlineData("payload.js")]
    [InlineData("no-extension")]
    [InlineData(null)]
    public void ForFileName_never_returns_a_browser_executable_type(string? fileName)
    {
        var contentType = DocumentContentTypes.ForFileName(fileName);

        Assert.Equal(DocumentContentTypes.Fallback, contentType);
        Assert.DoesNotContain("html", contentType, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("svg", contentType, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript", contentType, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("image/png", true)]
    [InlineData("image/jpeg", true)]
    [InlineData("text/plain; charset=utf-8", false)]
    [InlineData("text/html", false)]
    [InlineData("image/svg+xml", false)]
    [InlineData("application/octet-stream", false)]
    public void CanPreviewInline_allows_only_non_executable_types(string contentType, bool expected)
    {
        Assert.Equal(expected, DocumentContentTypes.CanPreviewInline(contentType));
    }
}
