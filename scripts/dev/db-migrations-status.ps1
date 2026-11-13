[CmdletBinding()]
param(
    [switch] $NoBuild,
    [string] $ReportDirectory = 'artifacts/ef-review'
)

$ErrorActionPreference = 'Stop'
try {
    . (Join-Path $PSScriptRoot '_dev-database-common.ps1')

    $arguments = @(
        '--environment', 'Development',
        '--expected-server', 'Shady',
        '--expected-database', 'NexusPmo'
    )
    $exitCode = Invoke-DevDatabaseGate `
        -Action inspect `
        -Operation migrations-status `
        -GateArguments $arguments `
        -ReportDirectory $ReportDirectory `
        -NoBuild:$NoBuild
    exit $exitCode
}
catch {
    if (Get-Command Write-DevDatabaseFailureReport -ErrorAction SilentlyContinue) {
        Write-DevDatabaseFailureReport -Operation 'migrations-status' -Message $_.Exception.Message -ReportDirectory $ReportDirectory
    }
    else {
        Write-Error $_.Exception.Message
    }
    exit 1
}
