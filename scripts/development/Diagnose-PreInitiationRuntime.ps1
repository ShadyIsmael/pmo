[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ProjectPublicId,

    [ValidateNotNullOrEmpty()]
    [string]$ApiBaseUrl = 'https://localhost:5001'
)

$ErrorActionPreference = 'Stop'
$parsedPublicId = [Guid]::Parse($ProjectPublicId)
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$sqlDiagnostic = Join-Path $repositoryRoot 'scripts\database\inspect-pre-initiation-state.sql'
$expectedApiAssembly = Join-Path $repositoryRoot 'apps\api\src\NEXUS.Api\bin\Debug\net10.0\NEXUS.Api.dll'

Write-Host '=== Running API process ==='
$apiProcesses = @()
try {
    $apiProcesses = @(Get-CimInstance -ClassName Win32_Process | Where-Object {
        $_.Name -eq 'NEXUS.Api.exe' -or $_.CommandLine -match '(?i)NEXUS\.Api\.dll'
    })
} catch {
    Write-Warning "Could not inspect process command lines in this account: $($_.Exception.Message)"
}

if ($apiProcesses.Count -eq 0) {
    Write-Warning 'No NEXUS.Api process was found. Check whether an API listener is running.'
} else {
    foreach ($apiProcess in $apiProcesses) {
        $loadedAssemblyPath = $null
        try {
            $loadedAssemblyPath = (Get-Process -Id $apiProcess.ProcessId -ErrorAction Stop).Modules |
                Where-Object { $_.ModuleName -ieq 'NEXUS.Api.dll' } |
                Select-Object -First 1 -ExpandProperty FileName
        } catch {
            Write-Warning "Could not enumerate loaded modules for PID $($apiProcess.ProcessId)."
        }

        if (-not $loadedAssemblyPath -and $apiProcess.CommandLine) {
            $match = [regex]::Match($apiProcess.CommandLine, '(?i)(?:"(?<path>[^"]*NEXUS\.Api\.dll)"|(?<path>[^\s"]*NEXUS\.Api\.dll))')
            if ($match.Success) {
                $loadedAssemblyPath = $match.Groups['path'].Value
                if (-not [IO.Path]::IsPathRooted($loadedAssemblyPath)) {
                    Write-Host "PID $($apiProcess.ProcessId): command line references relative assembly '$loadedAssemblyPath'."
                    $loadedAssemblyPath = $null
                }
            }
        }

        Write-Host "PID $($apiProcess.ProcessId); executable $($apiProcess.ExecutablePath); assembly $loadedAssemblyPath"
        if ($loadedAssemblyPath -and (Test-Path -LiteralPath $loadedAssemblyPath)) {
            $actualFile = Get-Item -LiteralPath $loadedAssemblyPath
            $actualHash = (Get-FileHash -LiteralPath $loadedAssemblyPath -Algorithm SHA256).Hash
            Write-Host "Loaded assembly SHA256 $actualHash; last write $($actualFile.LastWriteTime.ToString('o'))"
        }
    }
}

Write-Host "Expected standard Debug output: $expectedApiAssembly"
if (Test-Path -LiteralPath $expectedApiAssembly) {
    $expectedFile = Get-Item -LiteralPath $expectedApiAssembly
    $expectedHash = (Get-FileHash -LiteralPath $expectedApiAssembly -Algorithm SHA256).Hash
    Write-Host "Expected output SHA256 $expectedHash; last write $($expectedFile.LastWriteTime.ToString('o'))"
}

Write-Host '=== Read-only Development database diagnostic ==='
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw 'sqlcmd was not found. Run this diagnostic from the Product Owner PowerShell environment with sqlcmd installed.'
}
& sqlcmd -S Shady -d NexusPmo -E -b -W -s '|' -i $sqlDiagnostic
if ($LASTEXITCODE -ne 0) {
    throw "Read-only SQL diagnostic failed with sqlcmd exit code $LASTEXITCODE. The SQL script verifies SERVERPROPERTY('ServerName') and DB_NAME() before its SELECTs."
}

Write-Host '=== Authenticated project shell read ==='
Write-Host 'Enter a short-lived Development API bearer token at the hidden prompt. It is used only for this read request and is not saved.'
$secureToken = Read-Host 'Development API bearer token' -AsSecureString
$token = [System.Net.NetworkCredential]::new('', $secureToken).Password
try {
    $shell = Invoke-RestMethod -Method Get `
        -Uri "$($ApiBaseUrl.TrimEnd('/'))/api/projects/$($parsedPublicId.ToString('D'))/shell" `
        -Headers @{ Authorization = "Bearer $token" } `
        -TimeoutSec 30

    $shell | Select-Object id, publicId, name, phase, phaseId, phaseNameEn, phaseNameAr |
        ConvertTo-Json -Depth 5
} finally {
    $token = $null
    if ($secureToken) { $secureToken.Dispose() }
}
