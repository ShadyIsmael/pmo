function Get-DevDatabaseRepositoryRoot {
    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
}

function Initialize-DevDatabaseProcessEnvironment {
    param([Parameter(Mandatory = $true)][string] $RepositoryRoot)

    if ([string]::IsNullOrWhiteSpace($env:ASPNETCORE_ENVIRONMENT)) {
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
    }
    elseif ($env:ASPNETCORE_ENVIRONMENT -cne 'Development') {
        throw 'Development database scripts refuse ASPNETCORE_ENVIRONMENT other than Development.'
    }

    if ([string]::IsNullOrWhiteSpace($env:DOTNET_ENVIRONMENT)) {
        $env:DOTNET_ENVIRONMENT = 'Development'
    }
    elseif ($env:DOTNET_ENVIRONMENT -cne 'Development') {
        throw 'Development database scripts refuse DOTNET_ENVIRONMENT other than Development.'
    }

    if (-not [string]::IsNullOrWhiteSpace($env:NEXUS_EF_DEVELOPMENT_CONNECTION_STRING)) {
        return
    }

    # Respect the same environment connection overrides used by the API first.
    foreach ($name in @(
        'ConnectionStrings__DefaultConnection',
        'SQLCONNSTR_DefaultConnection',
        'CUSTOMCONNSTR_DefaultConnection'
    )) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $env:NEXUS_EF_DEVELOPMENT_CONNECTION_STRING = $value
            return
        }
    }

    $apiDirectory = Join-Path $RepositoryRoot 'apps\api\src\NEXUS.Api'
    $configuredConnection = $null
    foreach ($fileName in @('appsettings.json', 'appsettings.Development.json')) {
        $settingsPath = Join-Path $apiDirectory $fileName
        if (-not (Test-Path -LiteralPath $settingsPath -PathType Leaf)) {
            continue
        }

        $settings = Get-Content -Raw -Encoding UTF8 -LiteralPath $settingsPath | ConvertFrom-Json
        $candidate = [string]$settings.ConnectionStrings.DefaultConnection
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            $configuredConnection = $candidate
        }
    }

    if ([string]::IsNullOrWhiteSpace($configuredConnection)) {
        throw 'No Development connection was found. Set NEXUS_EF_DEVELOPMENT_CONNECTION_STRING securely in this PowerShell process.'
    }

    # Keep the connection value in-process only. It is never put in command arguments or reports.
    $env:NEXUS_EF_DEVELOPMENT_CONNECTION_STRING = $configuredConnection
}

function Resolve-DevDatabaseReviewArtifactPath {
    param(
        [Parameter(Mandatory = $true)][string] $RepositoryRoot,
        [Parameter(Mandatory = $true)][string] $Path
    )

    $candidate = if ([System.IO.Path]::IsPathRooted($Path)) {
        [System.IO.Path]::GetFullPath($Path)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $Path))
    }
    $artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot 'artifacts/ef-review')).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $allowedPrefix = $artifactRoot + [System.IO.Path]::DirectorySeparatorChar
    if (-not [string]::Equals($candidate, $artifactRoot, [StringComparison]::OrdinalIgnoreCase) -and
        -not $candidate.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Database runner files must stay under the repository artifacts/ef-review directory.'
    }
    return $candidate
}

function Get-DevDatabaseReportPath {
    param(
        [Parameter(Mandatory = $true)][string] $RepositoryRoot,
        [Parameter(Mandatory = $true)][string] $Operation,
        [string] $ReportDirectory = 'artifacts/ef-review'
    )

    $directoryPath = Resolve-DevDatabaseReviewArtifactPath -RepositoryRoot $RepositoryRoot -Path $ReportDirectory

    [System.IO.Directory]::CreateDirectory($directoryPath) | Out-Null
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
    $suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)
    return Join-Path $directoryPath "dev-db-$Operation-$stamp-$suffix.json"
}

function ConvertTo-DevDatabaseSafeOutput {
    param([AllowEmptyString()][string] $Text)

    if ([string]::IsNullOrEmpty($Text)) {
        return ''
    }

    $safe = [regex]::Replace(
        $Text,
        '(?i)\b(Password|Pwd|User\s*ID|UID|Access\s*Token|AccountKey)\s*=\s*[^;,\r\n]*',
        '$1=[REDACTED]'
    )
    return [regex]::Replace($safe, '(?im)^\s*(?:connection\s*string)\s*:\s*.*$', 'Connection string: [REDACTED]')
}

function Get-DevDatabaseAuthorizationFingerprint {
    param([AllowNull()][string] $AuthorizationReference)

    if ([string]::IsNullOrWhiteSpace($AuthorizationReference)) {
        return $null
    }

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($AuthorizationReference.Trim())
    return [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($bytes))
}

function Write-DevDatabaseFailureReport {
    param(
        [Parameter(Mandatory = $true)][string] $Operation,
        [Parameter(Mandatory = $true)][string] $Message,
        [string] $ReportDirectory = 'artifacts/ef-review',
        [string] $AuthorizationReference
    )

    $safeMessage = ConvertTo-DevDatabaseSafeOutput -Text $Message
    try {
        $root = Get-DevDatabaseRepositoryRoot
        $reportPath = Get-DevDatabaseReportPath -RepositoryRoot $root -Operation "$Operation-validation" -ReportDirectory $ReportDirectory
        $report = [ordered]@{
            reportVersion = 1
            operation = $Operation
            failureStage = 'wrapper-validation-or-launch'
            environment = $env:ASPNETCORE_ENVIRONMENT
            dotnetEnvironment = $env:DOTNET_ENVIRONMENT
            expectedServer = 'Shady'
            expectedDatabase = 'NexusPmo'
            completedUtc = [DateTime]::UtcNow.ToString('O')
            exitCode = 1
            authorizationReferenceSha256 = Get-DevDatabaseAuthorizationFingerprint -AuthorizationReference $AuthorizationReference
            output = $safeMessage
            connectionStringRecorded = $false
        }
        $json = $report | ConvertTo-Json -Depth 5
        [System.IO.File]::WriteAllText($reportPath, $json, [System.Text.UTF8Encoding]::new($false))
        Write-Host "Development database workflow stopped before successful gate completion: $safeMessage"
        Write-Host "Sanitized execution report: $reportPath"
    }
    catch {
        Write-Host "Development database workflow stopped before successful gate completion: $safeMessage"
        Write-Error 'The sanitized failure report could not be written.'
    }
}

function Invoke-DevDatabaseGate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][ValidateSet('model', 'inspect', 'apply')][string] $Action,
        [Parameter(Mandatory = $true)][string] $Operation,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]] $GateArguments,
        [string] $ReportDirectory = 'artifacts/ef-review',
        [string] $AuthorizationReference,
        [switch] $NoBuild
    )

    $root = Get-DevDatabaseRepositoryRoot
    Initialize-DevDatabaseProcessEnvironment -RepositoryRoot $root

    if ($PSVersionTable.PSVersion.Major -lt 7) {
        throw 'The Development database runner requires PowerShell 7 or later.'
    }

    $gateScript = Join-Path $root 'scripts\database\ef-database-gate.ps1'
    if (-not (Test-Path -LiteralPath $gateScript -PathType Leaf)) {
        throw "The repository EF gate is missing: $gateScript"
    }

    $pwshCommand = Get-Command pwsh -ErrorAction Stop
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $pwshCommand.Source
    $startInfo.WorkingDirectory = $root
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $startInfo.StandardErrorEncoding = [System.Text.Encoding]::UTF8

    foreach ($argument in @('-NoLogo', '-NoProfile', '-File', $gateScript, $Action)) {
        $startInfo.ArgumentList.Add($argument)
    }
    if ($NoBuild) {
        $startInfo.ArgumentList.Add('-NoBuild')
    }
    foreach ($argument in $GateArguments) {
        $startInfo.ArgumentList.Add($argument)
    }

    $startedUtc = [DateTime]::UtcNow
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw 'The EF database gate process could not be started.'
        }

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        $outputParts = @()
        if (-not [string]::IsNullOrWhiteSpace($stdout)) {
            $outputParts += $stdout
        }
        if (-not [string]::IsNullOrWhiteSpace($stderr)) {
            $outputParts += $stderr
        }
        $rawOutput = $outputParts -join [Environment]::NewLine
        $safeOutput = ConvertTo-DevDatabaseSafeOutput -Text $rawOutput
        $exitCode = $process.ExitCode
    }
    finally {
        $process.Dispose()
    }

    $reportPath = Get-DevDatabaseReportPath -RepositoryRoot $root -Operation $Operation -ReportDirectory $ReportDirectory
    $report = [ordered]@{
        reportVersion = 1
        operation = $Operation
        gateAction = $Action
        environment = 'Development'
        expectedServer = 'Shady'
        expectedDatabase = 'NexusPmo'
        startedUtc = $startedUtc.ToString('O')
        completedUtc = [DateTime]::UtcNow.ToString('O')
        exitCode = $exitCode
        authorizationReferenceSha256 = Get-DevDatabaseAuthorizationFingerprint -AuthorizationReference $AuthorizationReference
        output = $safeOutput
        connectionStringRecorded = $false
    }
    $json = $report | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText($reportPath, $json, [System.Text.UTF8Encoding]::new($false))

    if (-not [string]::IsNullOrWhiteSpace($safeOutput)) {
        Write-Host $safeOutput.TrimEnd()
    }
    Write-Host "Sanitized execution report: $reportPath"
    return [int]$exitCode
}
