[CmdletBinding()]
param(
    [switch] $NoBuild,
    [string] $ScriptOutput,
    [string] $ReportDirectory = 'artifacts/ef-review'
)

$ErrorActionPreference = 'Stop'
try {
    . (Join-Path $PSScriptRoot '_dev-database-common.ps1')

    $root = Get-DevDatabaseRepositoryRoot
    if ([string]::IsNullOrWhiteSpace($ScriptOutput)) {
        $stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
        $suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)
        $ScriptOutput = Join-Path $root "artifacts/ef-review/dev-pending-$stamp-$suffix.sql"
    }
    $ScriptOutput = Resolve-DevDatabaseReviewArtifactPath -RepositoryRoot $root -Path $ScriptOutput
    if (Test-Path -LiteralPath $ScriptOutput) {
        throw "Pending SQL output already exists; choose a new path so existing review evidence is preserved: $ScriptOutput"
    }
    $scriptOutputDirectory = Split-Path -Parent $ScriptOutput
    if (-not [string]::IsNullOrWhiteSpace($scriptOutputDirectory)) {
        [System.IO.Directory]::CreateDirectory($scriptOutputDirectory) | Out-Null
    }

    $modelArguments = @()
    $modelExitCode = Invoke-DevDatabaseGate `
        -Action model `
        -Operation preflight-model `
        -GateArguments $modelArguments `
        -ReportDirectory $ReportDirectory `
        -NoBuild:$NoBuild
    if ($modelExitCode -ne 0) {
        exit $modelExitCode
    }

    $inspectArguments = @(
        '--environment', 'Development',
        '--expected-server', 'Shady',
        '--expected-database', 'NexusPmo',
        '--script-output', $ScriptOutput
    )
    $inspectExitCode = Invoke-DevDatabaseGate `
        -Action inspect `
        -Operation preflight-inspect `
        -GateArguments $inspectArguments `
        -ReportDirectory $ReportDirectory `
        -NoBuild:$NoBuild
    if ($inspectExitCode -eq 0) {
        if (Test-Path -LiteralPath $ScriptOutput -PathType Leaf) {
            Write-Output "EF pending SQL generated for review: $ScriptOutput"
            Write-Output "Pending migration set and generated SQL are verified by the EF gate; review this exact file and calculate its SHA-256 before authorization."
        }
        else {
            Write-Output 'No pending migrations; the EF gate generated no SQL artifact.'
        }
    }
    elseif ($inspectExitCode -eq 2) {
        Write-Host "Inspection completed with pending migrations or schema differences (exit code 2). Review only the SQL file path printed above for this run; do not reuse an artifact from a previous or failed run."
    }
    else {
        Write-Host "Inspection failed (exit code $inspectExitCode). No SQL artifact was generated or refreshed by this run; do not review, hash, approve, or apply any previously generated SQL."
    }
    exit $inspectExitCode
}
catch {
    if (Get-Command Write-DevDatabaseFailureReport -ErrorAction SilentlyContinue) {
        Write-DevDatabaseFailureReport -Operation 'preflight' -Message $_.Exception.Message -ReportDirectory $ReportDirectory
    }
    else {
        Write-Error $_.Exception.Message
    }
    exit 1
}
