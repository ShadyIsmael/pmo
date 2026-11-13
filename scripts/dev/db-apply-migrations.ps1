[CmdletBinding()]
param(
    [string] $ReviewedSqlPath,
    [string] $ApprovedSha256,
    [string] $AuthorizationReference,
    [switch] $ConfirmDevelopmentApply,
    [string] $AllowUnrelatedBaselineDriftSha256,
    [string] $BackupDirectory,
    [switch] $NoBuild,
    [string] $ReportDirectory = 'artifacts/ef-review'
)

$ErrorActionPreference = 'Stop'
try {
    . (Join-Path $PSScriptRoot '_dev-database-common.ps1')

    if (-not $ConfirmDevelopmentApply) {
        throw 'Apply is blocked until -ConfirmDevelopmentApply is supplied after explicit authorization and SQL review.'
    }
    if ([string]::IsNullOrWhiteSpace($ReviewedSqlPath)) {
        throw 'Apply requires -ReviewedSqlPath. No database connection or write was attempted.'
    }
    if ($ApprovedSha256 -notmatch '^[A-Fa-f0-9]{64}$') {
        throw 'Apply requires a reviewed 64-character SHA-256. No database connection or write was attempted.'
    }
    if ($AllowUnrelatedBaselineDriftSha256 -and $AllowUnrelatedBaselineDriftSha256 -notmatch '^[A-Fa-f0-9]{64}$') {
        throw 'The unrelated baseline drift fingerprint must be a 64-character SHA-256 value.'
    }

    $root = Get-DevDatabaseRepositoryRoot
    $sqlPath = Resolve-DevDatabaseReviewArtifactPath -RepositoryRoot $root -Path $ReviewedSqlPath
    if (-not (Test-Path -LiteralPath $sqlPath -PathType Leaf)) {
        throw "Reviewed EF SQL file does not exist: $sqlPath"
    }

    $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sqlPath).Hash
    if (-not [string]::Equals($actualHash, $ApprovedSha256, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The reviewed SQL SHA-256 does not match the file. No database connection or write was attempted.'
    }
    if ([string]::IsNullOrWhiteSpace($AuthorizationReference)) {
        throw 'A reference to the active Product Owner authorization is required. The reference itself is not a substitute for that authorization.'
    }

    $arguments = @(
        '--environment', 'Development',
        '--expected-server', 'Shady',
        '--expected-database', 'NexusPmo',
        '--script-file', $sqlPath,
        '--approved-sha256', $actualHash
    )
    if ($AllowUnrelatedBaselineDriftSha256) {
        $arguments += @('--allow-unrelated-baseline-drift-sha256', $AllowUnrelatedBaselineDriftSha256)
    }
    if ($BackupDirectory) {
        if (-not [System.IO.Path]::IsPathRooted($BackupDirectory)) {
            throw 'When requested, the backup directory must be an absolute path visible to SQL Server.'
        }
        $arguments += @('--backup-directory', $BackupDirectory)
    }

    $exitCode = Invoke-DevDatabaseGate `
        -Action apply `
        -Operation migrations-apply `
        -GateArguments $arguments `
        -ReportDirectory $ReportDirectory `
        -AuthorizationReference $AuthorizationReference `
        -NoBuild:$NoBuild
    exit $exitCode
}
catch {
    if (Get-Command Write-DevDatabaseFailureReport -ErrorAction SilentlyContinue) {
        Write-DevDatabaseFailureReport -Operation 'migrations-apply' -Message $_.Exception.Message -ReportDirectory $ReportDirectory -AuthorizationReference $AuthorizationReference
    }
    else {
        Write-Error $_.Exception.Message
    }
    exit 1
}
