using Microsoft.Extensions.Configuration;

namespace NEXUS.Modules.Documents.Application;

public static class DocumentUploadLimits
{
    public const long DefaultMaximumBytes = 25L * 1024 * 1024;
    public const long MaximumConfiguredBytes = 100L * 1024 * 1024;
    public const long MultipartOverheadBytes = 1L * 1024 * 1024;

    public static long ResolveMaximumBytes(IConfiguration? configuration)
    {
        var configuredValue = configuration?["Storage:MaxUploadBytes"];
        if (string.IsNullOrWhiteSpace(configuredValue))
            return DefaultMaximumBytes;
        if (!long.TryParse(configuredValue, out var maximumBytes)
            || maximumBytes < 1
            || maximumBytes > MaximumConfiguredBytes)
            throw new InvalidOperationException($"Storage:MaxUploadBytes must be between 1 and {MaximumConfiguredBytes}.");
        return maximumBytes;
    }
}
