using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class TenantFallbackArchitectureTests
{
    private const string LegacyDemoTenantId = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public void Legacy_demo_tenant_guid_is_confined_to_seed_definitions()
    {
        var repositoryRoot = FindRepositoryRoot();
        var allowedFiles = new HashSet<string>(StringComparer.Ordinal)
        {
            "apps/api/src/NEXUS.Persistence/SeedDataProvider.cs",
            "apps/api/src/NEXUS.SharedKernel/Helpers/TenantConstants.cs"
        };

        var unexpectedOccurrences = EnumerateProductionSourceFiles(repositoryRoot)
            .SelectMany(file => ReadOccurrences(repositoryRoot, file, LegacyDemoTenantId))
            .Where(occurrence => !allowedFiles.Contains(occurrence.RelativePath))
            .ToArray();

        Assert.True(
            unexpectedOccurrences.Length == 0,
            "The legacy demo tenant ID must remain limited to seed definitions. Unexpected occurrences:\n" +
            string.Join(Environment.NewLine, unexpectedOccurrences.Select(FormatOccurrence)));
    }

    [Fact]
    public void Demo_tenant_constant_is_not_used_as_an_application_request_fallback()
    {
        var repositoryRoot = FindRepositoryRoot();
        var allowedFiles = new HashSet<string>(StringComparer.Ordinal)
        {
            "apps/api/src/NEXUS.Modules.Identity/Application/SeededDevelopmentAdmin.cs",
            "apps/api/src/NEXUS.SharedKernel/Helpers/TenantConstants.cs"
        };

        var unexpectedOccurrences = EnumerateProductionSourceFiles(repositoryRoot)
            .SelectMany(file => ReadOccurrences(repositoryRoot, file, "DemoTenantId"))
            .Where(occurrence => !allowedFiles.Contains(occurrence.RelativePath))
            .ToArray();

        Assert.True(
            unexpectedOccurrences.Length == 0,
            "DemoTenantId is reserved for the shared constant and seeded development identity. " +
            "Do not use it to resolve request or resource scope. Unexpected occurrences:\n" +
            string.Join(Environment.NewLine, unexpectedOccurrences.Select(FormatOccurrence)));
    }

    private static IEnumerable<string> EnumerateProductionSourceFiles(string repositoryRoot)
    {
        var sourceRoot = Path.Combine(repositoryRoot, "apps", "api", "src");
        return Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(sourceRoot, file)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj" or ".verification"));
    }

    private static IEnumerable<SourceOccurrence> ReadOccurrences(
        string repositoryRoot,
        string file,
        string token)
    {
        var relativePath = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
        var lines = File.ReadLines(file);
        var lineNumber = 0;

        foreach (var line in lines)
        {
            lineNumber++;
            if (line.Contains(token, StringComparison.Ordinal))
                yield return new SourceOccurrence(relativePath, lineNumber, line.Trim());
        }
    }

    private static string FindRepositoryRoot()
    {
        var candidates = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };

        foreach (var candidate in candidates)
        {
            DirectoryInfo? directory = new(candidate);
            while (directory is not null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "apps", "api", "src")))
                    return directory.FullName;

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing apps/api/src.");
    }

    private static string FormatOccurrence(SourceOccurrence occurrence) =>
        $"{occurrence.RelativePath}:{occurrence.LineNumber}: {occurrence.Line}";

    private sealed record SourceOccurrence(string RelativePath, int LineNumber, string Line);
}
