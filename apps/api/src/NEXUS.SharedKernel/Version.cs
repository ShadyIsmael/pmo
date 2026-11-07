using System.Text.Json;

namespace NEXUS.SharedKernel;

/// <summary>
/// Centralized FOPX PM product version metadata.
/// This is the single authoritative source for the product version,
/// shared by the backend, build metadata, and user-facing About screens.
/// </summary>
public static class ProductVersion
{
    public const string CurrentVersion = "1.0.0-rc.1";
    public const string ReleaseStatus = "release-candidate";
    public const string VersioningPolicy = "docs/releases/VERSIONING-POLICY.md";
    public const string ChangelogPath = "CHANGELOG.md";

    /// <summary>
    /// Loads version metadata from the repository-root <c>version.json</c> file.
    /// Returns a default instance when the file is missing (graceful degradation).
    /// </summary>
    public static ProductVersionInfo Load(string? repositoryRoot = null)
    {
        var root = repositoryRoot ?? FindRepositoryRoot();
        var path = root is null ? null : Path.Combine(root, "version.json");

        if (path is null || !File.Exists(path))
            return CreateDefault();

        try
        {
            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<VersionFile>(json, JsonOptions);
            if (parsed is null)
                return CreateDefault();

            return new ProductVersionInfo(
                string.IsNullOrWhiteSpace(parsed.Version) ? CurrentVersion : parsed.Version,
                parsed.BuildDate,
                parsed.GitCommit,
                string.IsNullOrWhiteSpace(parsed.Status) ? ReleaseStatus : parsed.Status,
                parsed.ReleaseNotes,
                parsed.Changelog,
                parsed.VersioningPolicy,
                parsed.ReleaseHistory);
        }
        catch (JsonException)
        {
            return CreateDefault();
        }
        catch (IOException)
        {
            return CreateDefault();
        }
    }

    /// <summary>
    /// Creates a default version info instance when no <c>version.json</c>
    /// is available. Build metadata fields are null to signal "unknown".
    /// </summary>
    public static ProductVersionInfo CreateDefault() =>
        new(
            CurrentVersion,
            null,
            null,
            ReleaseStatus,
            "docs/releases/1.0.0.md",
            ChangelogPath,
            VersioningPolicy,
            "docs/releases/RELEASE-HISTORY.md");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static string? FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory, "apps"))
                && (File.Exists(Path.Combine(directory, "version.json"))
                    || File.Exists(Path.Combine(directory, ".git"))))
                return directory;

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }

    private sealed record VersionFile
    {
        public string? Version { get; init; }
        public string? BuildDate { get; init; }
        public string? GitCommit { get; init; }
        public string? Status { get; init; }
        public string? ReleaseNotes { get; init; }
        public string? Changelog { get; init; }
        public string? VersioningPolicy { get; init; }
        public string? ReleaseHistory { get; init; }
    }
}

/// <summary>
/// Immutable snapshot of FOPX PM product version metadata.
/// </summary>
public sealed record ProductVersionInfo
{
    public string Version { get; }
    public string? BuildDate { get; }
    public string? GitCommit { get; }
    public string Status { get; }
    public string? ReleaseNotesPath { get; }
    public string? ChangelogPath { get; }
    public string? VersioningPolicyPath { get; }
    public string? ReleaseHistoryPath { get; }

    public ProductVersionInfo(
        string version,
        string? buildDate,
        string? gitCommit,
        string status,
        string? releaseNotesPath,
        string? changelogPath,
        string? versioningPolicyPath,
        string? releaseHistoryPath)
    {
        Version = version ?? throw new ArgumentNullException(nameof(version));
        BuildDate = buildDate;
        GitCommit = gitCommit;
        Status = status ?? throw new ArgumentNullException(nameof(status));
        ReleaseNotesPath = releaseNotesPath;
        ChangelogPath = changelogPath;
        VersioningPolicyPath = versioningPolicyPath;
        ReleaseHistoryPath = releaseHistoryPath;
    }

    public string DisplayVersion => string.IsNullOrEmpty(Version) ? ProductVersion.CurrentVersion : Version;

    public string ShortGitCommit => string.IsNullOrEmpty(GitCommit) ? "unknown" : GitCommit.Length > 7 ? GitCommit[..7] : GitCommit;

    public string BuildDateDisplay => string.IsNullOrEmpty(BuildDate) ? "unknown" : BuildDate;

    public string StatusDisplay => string.IsNullOrEmpty(Status) ? ProductVersion.ReleaseStatus : Status;
}