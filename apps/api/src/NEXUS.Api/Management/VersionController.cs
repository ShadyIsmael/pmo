using Microsoft.AspNetCore.Mvc;
using NEXUS.SharedKernel;

namespace NEXUS.Api.Management;

/// <summary>
/// Exposes centralized FOPX PM product version metadata.
/// This is the single authoritative source for the product version,
/// shared by the frontend, build metadata, and user-facing About screens.
/// </summary>
[ApiController]
[Route("api/version")]
[Produces("application/problem+json")]
public sealed class VersionController : ControllerBase
{
    private readonly ProductVersionInfo _version;

    public VersionController(ProductVersionInfo version)
    {
        _version = version ?? throw new ArgumentNullException(nameof(version));
    }

    /// <summary>
    /// Returns the current FOPX PM product version metadata.
    /// Build metadata (build date, Git commit) may be null when unavailable.
    /// </summary>
    [HttpGet]
    [Produces("application/json")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public ActionResult<VersionDto> Get()
    {
        return Ok(new VersionDto(
            Product: "FOPX PM",
            FullName: "FOPX Project Management",
            Version: _version.Version,
            BuildDate: _version.BuildDate,
            GitCommit: _version.GitCommit,
            ShortGitCommit: _version.ShortGitCommit,
            Status: _version.Status,
            ReleaseNotesPath: _version.ReleaseNotesPath,
            ChangelogPath: _version.ChangelogPath,
            VersioningPolicyPath: _version.VersioningPolicyPath,
            ReleaseHistoryPath: _version.ReleaseHistoryPath));
    }
}

/// <summary>
/// DTO for the product version endpoint response.
/// </summary>
public sealed record VersionDto(
    string Product,
    string FullName,
    string Version,
    string? BuildDate,
    string? GitCommit,
    string ShortGitCommit,
    string Status,
    string? ReleaseNotesPath,
    string? ChangelogPath,
    string? VersioningPolicyPath,
    string? ReleaseHistoryPath);