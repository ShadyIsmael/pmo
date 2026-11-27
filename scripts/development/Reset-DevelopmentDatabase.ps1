#requires -Version 7.0
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$efGateScript = Join-Path $repositoryRoot 'scripts\database\ef-database-gate.ps1'
$apiProject = Join-Path $repositoryRoot 'apps\api\src\NEXUS.Api\NEXUS.Api.csproj'
$designTimeProject = Join-Path $repositoryRoot 'apps\api\tools\NEXUS.Persistence.DesignTime\NEXUS.Persistence.DesignTime.csproj'
$expectedServer = 'Shady'
$expectedDatabase = 'NexusPmo'
$authorizationReference = 'Active Product Owner instruction in this conversation dated 2026-11-24: authorize complete Development reset of Shady/NexusPmo.'
$processEnvironmentNames = @(
    'ASPNETCORE_ENVIRONMENT',
    'DOTNET_ENVIRONMENT',
    'NEXUS_EF_DEVELOPMENT_CONNECTION_STRING',
    'NEXUS_ADMIN_BOOTSTRAP_PASSWORD'
)
$originalProcessEnvironment = @{}
foreach ($name in $processEnvironmentNames) {
    $originalProcessEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, [EnvironmentVariableTarget]::Process)
}

function ConvertFrom-SecureStringValue {
    [CmdletBinding()]
    param([Parameter(Mandatory)][Security.SecureString] $Value)

    $pointer = [IntPtr]::Zero
    try {
        $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        if ($pointer -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
        }
    }
}

function Read-HiddenValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $Prompt,
        [int] $MinimumLength = 1,
        [switch] $Confirm
    )

    $first = Read-Host -Prompt $Prompt -AsSecureString
    $second = $null
    $firstText = $null
    $secondText = $null
    try {
        if ($first.Length -lt $MinimumLength) {
            throw "The value must contain at least $MinimumLength characters."
        }
        $firstText = ConvertFrom-SecureStringValue -Value $first
        if ($Confirm) {
            $second = Read-Host -Prompt 'Confirm the value' -AsSecureString
            $secondText = ConvertFrom-SecureStringValue -Value $second
            if (-not [string]::Equals($firstText, $secondText, [StringComparison]::Ordinal)) {
                throw 'The two hidden values did not match.'
            }
        }
        return $firstText
    }
    finally {
        if ($first) { $first.Dispose() }
        if ($second) { $second.Dispose() }
        $secondText = $null
    }
}

function Assert-ValidDevelopmentConnectionString {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string] $Value)

    # Sanitized validation only: the supplied value is never echoed in any error message.
    $example = 'Server=<server>;Database=<database>;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true'
    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "The Development connection string is empty. Use the approved Windows Authentication format, for example: $example"
    }

    # Tokenize on ';' while honoring single/double-quoted values (a quoted value may contain ';' or '=').
    $segments = New-Object System.Collections.Generic.List[string]
    $current = New-Object System.Text.StringBuilder
    $quote = [char]0
    foreach ($ch in $Value.ToCharArray()) {
        if ($quote -ne [char]0) {
            [void]$current.Append($ch)
            if ($ch -eq $quote) { $quote = [char]0 }
        }
        elseif ($ch -eq "'" -or $ch -eq '"') {
            $quote = $ch
            [void]$current.Append($ch)
        }
        elseif ($ch -eq ';') {
            $segments.Add($current.ToString())
            $current.Clear() | Out-Null
        }
        else {
            [void]$current.Append($ch)
        }
    }
    $segments.Add($current.ToString())

    $pairs = @{}
    foreach ($segment in $segments) {
        if ([string]::IsNullOrWhiteSpace($segment)) { continue }
        $eq = $segment.IndexOf('=')
        if ($eq -le 0) {
            throw "The Development connection string is not in 'key=value;key=value' format (a segment has no '='). The supplied value was not printed. Approved Windows Authentication example: $example"
        }
        $key = $segment.Substring(0, $eq).Trim().Trim('"').Trim("'").ToLowerInvariant()
        $val = $segment.Substring($eq + 1).Trim()
        if ($val.Length -ge 2 -and (($val[0] -eq '"' -and $val[$val.Length - 1] -eq '"') -or ($val[0] -eq "'" -and $val[$val.Length - 1] -eq "'"))) {
            $val = $val.Substring(1, $val.Length - 2)
        }
        if ([string]::IsNullOrWhiteSpace($key)) {
            throw "The Development connection string contains an empty key. The supplied value was not printed. Approved Windows Authentication example: $example"
        }
        $pairs[$key] = $val
    }

    $server = $null
    foreach ($k in @('server', 'data source', 'datasource', 'address', 'addr', 'network address')) {
        if ($pairs.ContainsKey($k)) { $server = $pairs[$k]; break }
    }
    $database = $null
    foreach ($k in @('database', 'initial catalog')) {
        if ($pairs.ContainsKey($k)) { $database = $pairs[$k]; break }
    }
    if ([string]::IsNullOrWhiteSpace($server) -or [string]::IsNullOrWhiteSpace($database)) {
        throw "The Development connection string must include a Server (Data Source) and a Database (Initial Catalog). The supplied value was not printed. Approved Windows Authentication example: $example"
    }

    $auth = $null
    foreach ($k in @('integrated security', 'trusted_connection')) {
        if ($pairs.ContainsKey($k)) { $auth = $pairs[$k]; break }
    }
    if ([string]::IsNullOrWhiteSpace($auth) -or $auth.ToLowerInvariant() -notin @('true', 'yes', 'sspi')) {
        throw "The Development connection string must use Windows Authentication (Integrated Security=True or Trusted_Connection=True). The supplied value was not printed. Approved example: $example"
    }
    if ($pairs.ContainsKey('password') -and -not [string]::IsNullOrWhiteSpace($pairs['password'])) {
        throw 'Do not include a Password in the Development connection string; use Windows Authentication only. The supplied value was not printed.'
    }

    return [pscustomobject]@{ Server = $server; Database = $database }
}

function Invoke-CheckedScript {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $FilePath,
        [Parameter(Mandatory)][string[]] $Arguments,
        [Parameter(Mandatory)][string] $Description
    )

    Write-Host $Description
    & $FilePath @Arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "$Description failed with exit code $exitCode. No later reset step was started."
    }
}

try {
    foreach ($name in @('ASPNETCORE_ENVIRONMENT', 'DOTNET_ENVIRONMENT')) {
        $existingEnvironment = [Environment]::GetEnvironmentVariable($name, [EnvironmentVariableTarget]::Process)
        if (-not [string]::IsNullOrWhiteSpace($existingEnvironment) -and -not [string]::Equals($existingEnvironment, 'Development', [StringComparison]::OrdinalIgnoreCase)) {
            throw "$name is set to a non-Development value. Start a fresh PowerShell process for this Development-only reset."
        }
    }

    foreach ($requiredPath in @($efGateScript, $apiProject, $designTimeProject)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Required repository file was not found: $requiredPath"
        }
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'The .NET SDK was not found on PATH.'
    }

    [Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Development', [EnvironmentVariableTarget]::Process)
    [Environment]::SetEnvironmentVariable('DOTNET_ENVIRONMENT', 'Development', [EnvironmentVariableTarget]::Process)

    Invoke-CheckedScript -FilePath $efGateScript -Arguments @('model') -Description 'Checking EF model and migration snapshot offline.'

    Write-Host 'Building the isolated EF design-time project for a full empty-database migration review.'
    & dotnet restore $designTimeProject --ignore-failed-sources --disable-parallel -m:1
    if ($LASTEXITCODE -ne 0) { throw "EF design-time restore failed with exit code $LASTEXITCODE." }
    & dotnet build $designTimeProject --no-restore --verbosity minimal -m:1
    if ($LASTEXITCODE -ne 0) { throw "EF design-time build failed with exit code $LASTEXITCODE." }

    $reviewDirectory = Join-Path ([System.IO.Path]::GetTempPath()) 'FopxDevelopmentDatabaseResetReview'
    New-Item -ItemType Directory -Path $reviewDirectory -Force | Out-Null
    $reviewFileName = 'NexusPmo-empty-database-migrations-{0}.sql' -f [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
    $migrationReviewPath = Join-Path $reviewDirectory $reviewFileName
    $persistenceProject = Join-Path $repositoryRoot 'apps\api\src\NEXUS.Persistence\NEXUS.Persistence.csproj'
    $migrationScriptArguments = @(
        'ef', 'migrations', 'script', '0',
        '--project', $persistenceProject,
        '--startup-project', $designTimeProject,
        '--context', 'NEXUS.Persistence.AppDbContext',
        '--output', $migrationReviewPath,
        '--no-build'
    )
    & dotnet @migrationScriptArguments
    if ($LASTEXITCODE -ne 0) { throw "Full empty-database migration SQL generation failed with exit code $LASTEXITCODE." }
    if (-not (Test-Path -LiteralPath $migrationReviewPath -PathType Leaf)) {
        throw 'EF did not create the full empty-database migration review file.'
    }
    $migrationReviewHash = (Get-FileHash -LiteralPath $migrationReviewPath -Algorithm SHA256).Hash
    Write-Host "Full empty-database migration SQL: $migrationReviewPath"
    Write-Host "SHA-256: $migrationReviewHash"
    Write-Host 'Review the complete SQL file before continuing. Type the displayed SHA-256 to confirm you reviewed this exact file.'
    $reviewConfirmation = Read-Host 'Reviewed SQL SHA-256'
    if (-not [string]::Equals($reviewConfirmation.Trim(), $migrationReviewHash, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Migration SQL review was not confirmed. The Development database was not reset.'
    }

    Write-Host 'Building the Development API so the reset gate can verify and run its official Minimal Seed entrypoint before deletion.'
    & dotnet build $apiProject --configuration Debug --no-restore --verbosity minimal -m:1
    if ($LASTEXITCODE -ne 0) { throw "Development API build failed with exit code $LASTEXITCODE. The database was not reset." }

    $connectionString = [Environment]::GetEnvironmentVariable('NEXUS_EF_DEVELOPMENT_CONNECTION_STRING', [EnvironmentVariableTarget]::Process)
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        $connectionString = Read-HiddenValue -Prompt 'Enter the active Development API SQL connection string (hidden input)' -MinimumLength 1
        [Environment]::SetEnvironmentVariable('NEXUS_EF_DEVELOPMENT_CONNECTION_STRING', $connectionString, [EnvironmentVariableTarget]::Process)
    }

    $configuredOverride = [Environment]::GetEnvironmentVariable('ConnectionStrings__DefaultConnection', [EnvironmentVariableTarget]::Process)
    if ($null -ne $configuredOverride -and [string]::IsNullOrWhiteSpace($configuredOverride)) {
        throw 'ConnectionStrings__DefaultConnection is set to an empty value in this process; it overrides appsettings.Development.json and leaves the API Development endpoint empty. Remove that variable or set it to the active Development connection string before rerunning.'
    }

    $validatedTarget = Assert-ValidDevelopmentConnectionString -Value $connectionString
    Write-Host ("Development connection string accepted (sanitized): server={0}; database={1}; Windows Authentication." -f $validatedTarget.Server, $validatedTarget.Database)

    $targetProbeArguments = @(
        'inspect',
        '--environment', 'Development',
        '--expected-server', $expectedServer,
        '--expected-database', $expectedDatabase
    )
    Write-Host 'Running the read-only live target probe before collecting the Admin bootstrap password.'
    $targetProbeOutput = @(& $efGateScript @targetProbeArguments 2>&1)
    $targetProbeExitCode = $LASTEXITCODE
    $targetProbeText = ($targetProbeOutput | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
    if (-not [string]::IsNullOrWhiteSpace($targetProbeText)) {
        Write-Host $targetProbeText
    }
    if ($targetProbeExitCode -notin @(0, 2)) {
        throw "The read-only Development target probe failed with exit code $targetProbeExitCode. No bootstrap password was requested and no reset was started."
    }
    $hasDevelopmentEnvironment = $targetProbeText -match '(?im)^Environment: Development\s*$'
    $hasExpectedLiveDatabaseIdentity = $targetProbeText -match '(?im)^Target Development DB verified: Yes \(server=Shady; database=NexusPmo; databaseId=[1-9][0-9]*\)\s*$'
    $hasSynchronizedModel = $targetProbeText -match '(?im)^EF Model synchronized: Yes\s*$'
    if (-not $hasDevelopmentEnvironment -or -not $hasExpectedLiveDatabaseIdentity -or -not $hasSynchronizedModel) {
        throw 'The live Development target, existing database identity, or synchronized EF model was not confirmed. No bootstrap password was requested and no reset was started.'
    }

    Write-Warning 'The reset will delete all existing Development data in Shady/NexusPmo. Development backup is optional; no backup was requested.'
    $typedDatabaseName = Read-Host "Type $expectedDatabase to confirm the exact Development database"
    if (-not [string]::Equals($typedDatabaseName, $expectedDatabase, [StringComparison]::Ordinal)) {
        throw 'Database-name confirmation did not match. The Development database was not reset.'
    }

    $bootstrapPassword = Read-HiddenValue -Prompt 'New Admin bootstrap password (hidden; minimum 12 characters)' -MinimumLength 12 -Confirm
    [Environment]::SetEnvironmentVariable('NEXUS_ADMIN_BOOTSTRAP_PASSWORD', $bootstrapPassword, [EnvironmentVariableTarget]::Process)

    $resetArguments = @(
        'reset',
        '--environment', 'Development',
        '--expected-server', $expectedServer,
        '--expected-database', $expectedDatabase,
        '--confirm-reset-database', $expectedDatabase,
        '--product-owner-authorization', $authorizationReference
    )
    Invoke-CheckedScript -FilePath $efGateScript -Arguments $resetArguments -Description 'Verifying Shady/NexusPmo, resetting Development, applying all EF migrations, and running Minimal Seed twice.'

    Write-Host 'Reset completed. The EF gate has verified the post-reset database identity, full migration history, physical schema, one active Admin on the canonical Tenant, the NEXUS Organization and organization-role scope, the protected Admin permission baseline, no business/demo rows, and stable counts across both Minimal Seed runs.'
    Write-Host 'Admin login identifier: admin@nexus.com. Minimal Seed hashes the supplied bootstrap secret with ASP.NET Core Identity and requires a password change; the default temporary-password expiry is 24 hours.'
    Write-Host "If that password cannot be used after the successful reset, run '$PSScriptRoot\Recover-DevelopmentAdminPassword.ps1' and follow its hidden secure prompts."
}
finally {
    foreach ($name in $processEnvironmentNames) {
        [Environment]::SetEnvironmentVariable($name, $originalProcessEnvironment[$name], [EnvironmentVariableTarget]::Process)
    }
    $connectionString = $null
    $bootstrapPassword = $null
    $reviewConfirmation = $null
}
