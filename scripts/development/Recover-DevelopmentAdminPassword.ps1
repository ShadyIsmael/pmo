#requires -Version 7.0
[CmdletBinding()]
param()

$script:DevelopmentAdminRecoveryScriptRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)

function Get-DevelopmentAdminRecoveryPaths {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $ScriptRoot,
        [string] $RuntimeDirectory
    )

    $repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $ScriptRoot '..\..'))
    $apiProjectDirectory = Join-Path $repositoryRoot 'apps\api\src\NEXUS.Api'
    if ([string]::IsNullOrWhiteSpace($RuntimeDirectory)) {
        $RuntimeDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('FopxAdminRecoveryRuntime-' + [guid]::NewGuid().ToString('N'))
    }
    $runtimeDirectory = [System.IO.Path]::GetFullPath($RuntimeDirectory)
    [pscustomobject]@{
        RepositoryRoot = $repositoryRoot
        ApiProjectDirectory = $apiProjectDirectory
        ApiProject = Join-Path $apiProjectDirectory 'NEXUS.Api.csproj'
        ApiRuntimeDirectory = $runtimeDirectory
        ApiAssembly = Join-Path $runtimeDirectory 'NEXUS.Api.dll'
        ApiDepsFile = Join-Path $runtimeDirectory 'NEXUS.Api.deps.json'
        ApiRuntimeConfig = Join-Path $runtimeDirectory 'NEXUS.Api.runtimeconfig.json'
    }
}

function Invoke-DevelopmentAdminRecoveryRuntimePublish {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $DotnetPath,
        [Parameter(Mandatory)][string] $ApiProject,
        [Parameter(Mandatory)][string] $ApiProjectDirectory,
        [Parameter(Mandatory)][string] $OutputDirectory
    )

    if (Test-Path -LiteralPath $OutputDirectory) {
        throw 'The isolated Development recovery runtime directory already exists; refusing to reuse a possibly stale output.'
    }

    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
    $buildOutputDirectory = Join-Path $OutputDirectory 'build'
    $buildPropertiesPath = Join-Path $OutputDirectory 'DevelopmentAdminRecovery.Build.props'
    $buildOutputXml = [System.Security.SecurityElement]::Escape($buildOutputDirectory + [System.IO.Path]::DirectorySeparatorChar)
    $buildProperties = @'
<Project>
  <PropertyGroup Condition="'$(MSBuildProjectName)' == 'NEXUS.Api'">
    <OutputPath>__NEXUS_API_OUTPUT_PATH__</OutputPath>
  </PropertyGroup>
</Project>
'@
    $buildProperties = $buildProperties.Replace('__NEXUS_API_OUTPUT_PATH__', $buildOutputXml)
    Set-Content -LiteralPath $buildPropertiesPath -Value $buildProperties -Encoding utf8
    $publishArguments = @(
        'publish',
        $ApiProject,
        '--configuration', 'Debug',
        '--output', $OutputDirectory,
        '--no-restore',
        '--maxcpucount:1',
        "-p:CustomAfterMicrosoftCommonProps=$buildPropertiesPath",
        '-p:BuildProjectReferences=false',
        '-p:BuildInParallel=false',
        '--verbosity', 'quiet'
    )

    $locationPushed = $false
    $publishOutput = @()
    $publishExitCode = 1
    try {
        Push-Location -LiteralPath $ApiProjectDirectory
        $locationPushed = $true
        $publishOutput = @(& $DotnetPath @publishArguments 2>&1)
        $publishExitCode = $LASTEXITCODE
    }
    finally {
        if ($locationPushed) {
            Pop-Location
        }
        if (Test-Path -LiteralPath $buildOutputDirectory) {
            Remove-Item -LiteralPath $buildOutputDirectory -Recurse -Force -ErrorAction SilentlyContinue
        }
        if (Test-Path -LiteralPath $buildPropertiesPath) {
            Remove-Item -LiteralPath $buildPropertiesPath -Force -ErrorAction SilentlyContinue
        }
    }

    if ($publishExitCode -ne 0) {
        foreach ($line in ($publishOutput | Select-Object -Last 25)) {
            Write-DevelopmentAdminRecoveryDiagnostics -Text $line.ToString()
        }
        throw "The isolated Development API runtime publish failed (exit code: $publishExitCode). No password was requested or sent."
    }

    [pscustomobject]@{
        Arguments = $publishArguments
        Output = $publishOutput
        BuildOutputDirectory = $buildOutputDirectory
        BuildPropertiesPath = $buildPropertiesPath
        PublishOutputDirectory = $OutputDirectory
    }
}

function Assert-DevelopmentAdminRecoveryRuntimeComplete {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string] $RuntimeDirectory)

    $requiredFiles = @(
        'NEXUS.Api.dll',
        'NEXUS.Api.deps.json',
        'NEXUS.Api.runtimeconfig.json'
    )
    foreach ($fileName in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $RuntimeDirectory $fileName) -PathType Leaf)) {
            throw "The isolated Development API runtime is incomplete ($fileName is missing). No password was requested or sent."
        }
    }

    try {
        $dependencyManifest = Get-Content -LiteralPath (Join-Path $RuntimeDirectory 'NEXUS.Api.deps.json') -Raw | ConvertFrom-Json -AsHashtable
    }
    catch {
        throw 'The isolated Development API dependency manifest is invalid. No password was requested or sent.'
    }

    $runtimeTargetName = $dependencyManifest.runtimeTarget.name
    if ([string]::IsNullOrWhiteSpace($runtimeTargetName) -or -not $dependencyManifest.targets.Contains($runtimeTargetName)) {
        throw 'The isolated Development API dependency manifest does not select a runtime target. No password was requested or sent.'
    }

    $runtimeTarget = $dependencyManifest.targets[$runtimeTargetName]
    $mppAdapterLibrary = @($runtimeTarget.Keys | Where-Object { $_ -like 'NEXUS.Modules.ProjectPlanning.MppAdapter/*' })
    if ($mppAdapterLibrary.Count -ne 1) {
        throw 'The isolated Development API dependency manifest does not include exactly one MppAdapter project reference. No password was requested or sent.'
    }
    $mppAdapterAssets = @($runtimeTarget[$mppAdapterLibrary[0]].runtime.Keys)
    if ($mppAdapterAssets -notcontains 'NEXUS.Modules.ProjectPlanning.MppAdapter.dll') {
        throw 'The isolated Development API dependency manifest does not map the MppAdapter runtime assembly. No password was requested or sent.'
    }

    foreach ($libraryName in @('NEXUS.Modules.ProjectPlanning', 'MPXJ.Net', 'mpxj', 'IKVM.ByteCode', 'System.Drawing.Common')) {
        $matchingLibraries = @($runtimeTarget.Keys | Where-Object { $_ -like "$libraryName/*" })
        if ($matchingLibraries.Count -ne 1) {
            throw "The isolated Development API dependency manifest is missing the required $libraryName transitive runtime library. No password was requested or sent."
        }
    }

    $runtimeAssetCount = 0
    $missingAssets = [System.Collections.Generic.List[string]]::new()
    foreach ($library in $runtimeTarget.Values) {
        foreach ($assetKind in @('runtime', 'native', 'resources')) {
            if (-not $library.ContainsKey($assetKind)) {
                continue
            }
            foreach ($assetPath in $library[$assetKind].Keys) {
                if ($assetPath -eq '_._') {
                    continue
                }
                $runtimeAssetCount++
                $normalizedAssetPath = $assetPath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
                $assetCandidates = @((Join-Path $RuntimeDirectory $normalizedAssetPath))
                $assetName = [System.IO.Path]::GetFileName($normalizedAssetPath)
                if ($assetName -ne $normalizedAssetPath) {
                    $assetCandidates += Join-Path $RuntimeDirectory $assetName
                }
                if ($assetKind -eq 'resources') {
                    $cultureDirectory = [System.IO.Path]::GetFileName([System.IO.Path]::GetDirectoryName($normalizedAssetPath))
                    if (-not [string]::IsNullOrWhiteSpace($cultureDirectory)) {
                        $assetCandidates += Join-Path (Join-Path $RuntimeDirectory $cultureDirectory) $assetName
                    }
                }
                if (-not ($assetCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1)) {
                    $missingAssets.Add(('{0}:{1}' -f $assetKind, $assetPath))
                }
            }
        }
    }

    if ($missingAssets.Count -gt 0) {
        $missingSummary = ($missingAssets | Select-Object -First 8) -join ', '
        throw "The isolated Development API runtime is missing $($missingAssets.Count) dependency asset(s): $missingSummary. No password was requested or sent."
    }

    [pscustomobject]@{
        RuntimeTarget = $runtimeTargetName
        RuntimeAssetCount = $runtimeAssetCount
        MppAdapterAssembly = Join-Path $RuntimeDirectory 'NEXUS.Modules.ProjectPlanning.MppAdapter.dll'
    }
}

function Remove-DevelopmentAdminRecoveryRuntime {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string] $RuntimeDirectory)

    $fullRuntimeDirectory = [System.IO.Path]::GetFullPath($RuntimeDirectory)
    $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $runtimeDirectoryName = [System.IO.Path]::GetFileName($fullRuntimeDirectory)
    $isInsideTemp = $fullRuntimeDirectory.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase)
    $hasExpectedName = $runtimeDirectoryName -match '^FopxAdminRecoveryRuntime-[0-9a-f]{32}$'
    if (-not $isInsideTemp -or -not $hasExpectedName) {
        Write-Host 'The temporary recovery runtime was retained because its path did not match the isolated temp-directory guard.'
        return
    }

    if (Test-Path -LiteralPath $fullRuntimeDirectory) {
        Remove-Item -LiteralPath $fullRuntimeDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function New-DevelopmentAdminRecoveryStartInfo {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $DotnetPath,
        [Parameter(Mandatory)][string] $ApiRuntimeDirectory,
        [Parameter(Mandatory)][string] $ApiAssembly,
        [Parameter(Mandatory)][string] $ConnectionString
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $DotnetPath
    # Run the already-built API assembly directly. This avoids dotnet run and
    # launchSettings.json so the child receives only the explicit environment below.
    $startInfo.WorkingDirectory = $ApiRuntimeDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    @(
        $ApiAssembly,
        '--recover-development-admin-password',
        '--admin-recovery-expected-sql-server', 'Shady',
        '--admin-recovery-expected-sql-database', 'NexusPmo'
    ) | ForEach-Object { $startInfo.ArgumentList.Add([string]$_) }

    $startInfo.Environment['DOTNET_ENVIRONMENT'] = 'Development'
    $startInfo.Environment['ASPNETCORE_ENVIRONMENT'] = 'Development'
    $startInfo.Environment['NEXUS_EF_DEVELOPMENT_CONNECTION_STRING'] = $ConnectionString
    [void]$startInfo.Environment.Remove('FOPX_RECOVERY_MOCK_MODE')
    return $startInfo
}

function Protect-DevelopmentAdminRecoveryDiagnostic {
    [CmdletBinding()]
    param([AllowEmptyString()][string] $Text)

    $safeText = $Text
    $safeText = [regex]::Replace($safeText, '(?i)\bNEXUS_EF_DEVELOPMENT_CONNECTION_STRING\s*[:=]\s*.*$', 'NEXUS_EF_DEVELOPMENT_CONNECTION_STRING=<redacted>')
    $safeText = [regex]::Replace($safeText, '(?i)\b(?:server|data source)\s*=.*$', '[database connection details redacted]')
    $safeText = [regex]::Replace($safeText, '(?i)\b(password|pwd|clientsecret|client_secret|access[_ -]?token|refresh[_ -]?token)\s*[:=]\s*[^;,\s]+', '$1=<redacted>')
    return $safeText
}

function Write-DevelopmentAdminRecoveryDiagnostics {
    [CmdletBinding()]
    param([AllowNull()][string] $Text)

    if ([string]::IsNullOrEmpty($Text)) {
        return
    }
    foreach ($line in ($Text -split '\r?\n')) {
        if (-not [string]::IsNullOrWhiteSpace($line)) {
            Write-Host (Protect-DevelopmentAdminRecoveryDiagnostic -Text $line)
        }
    }
}

function New-DevelopmentAdminRecoveryPreflightResult {
    [CmdletBinding()]
    param(
        [bool] $InputReady,
        [int] $ExitCode,
        [bool] $TimedOut = $false
    )

    if ($TimedOut) {
        return [pscustomobject]@{
            Succeeded = $false
            ErrorMessage = 'Development Admin recovery preflight timed out before password collection. The child process was stopped; no password was requested or sent.'
        }
    }
    if ($InputReady) {
        return [pscustomobject]@{ Succeeded = $true; ErrorMessage = $null }
    }
    return [pscustomobject]@{
        Succeeded = $false
        ErrorMessage = "Development Admin recovery preflight exited before password collection (child exit code: $ExitCode). Review the safe child diagnostics above; no password was requested or sent."
    }
}

function Read-DevelopmentAdminRecoverySecureInputs {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][pscustomobject] $PreflightResult,
        [scriptblock] $SecureReader = {
            param([string] $PromptText)
            Read-Host -Prompt $PromptText -AsSecureString
        }
    )

    if (-not $PreflightResult.Succeeded) {
        throw $PreflightResult.ErrorMessage
    }

    $prompts = @(
        'New temporary Admin password',
        'Confirm temporary Admin password',
        'New permanent Admin password',
        'Confirm permanent Admin password'
    )
    $values = [System.Collections.Generic.List[System.Security.SecureString]]::new()
    try {
        foreach ($promptText in $prompts) {
            $secureValue = & $SecureReader $promptText
            if ($null -eq $secureValue) {
                throw 'Secure password input was canceled. No password was sent to the API.'
            }
            $values.Add($secureValue)
        }
        return $values.ToArray()
    }
    catch {
        foreach ($secureValue in $values) {
            $secureValue.Dispose()
        }
        throw
    }
}

function ConvertTo-SecurePasswordPipeLine {
    [CmdletBinding()]
    param([Parameter(Mandatory)][System.Security.SecureString] $SecureValue)

    $bstr = [IntPtr]::Zero
    $plainText = $null
    $utf8Bytes = $null
    try {
        $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
        $plainText = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
        $utf8Bytes = [System.Text.Encoding]::UTF8.GetBytes($plainText)
        return [Convert]::ToBase64String($utf8Bytes)
    }
    finally {
        if ($utf8Bytes) {
            [Array]::Clear($utf8Bytes, 0, $utf8Bytes.Length)
        }
        $plainText = $null
        if ($bstr -ne [IntPtr]::Zero) {
            [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
    }
}

function ConvertTo-DevelopmentAdminRecoveryPipeLines {
    [CmdletBinding()]
    param([Parameter(Mandatory)][System.Security.SecureString[]] $SecureInputs)

    if ($SecureInputs.Count -ne 4) {
        throw 'The secure recovery input set is incomplete. No password was sent to the API.'
    }

    $encodedInputs = [string[]]::new(4)
    try {
        for ($index = 0; $index -lt 4; $index++) {
            $encodedInputs[$index] = ConvertTo-SecurePasswordPipeLine -SecureValue $SecureInputs[$index]
        }
        if ($encodedInputs[0] -cne $encodedInputs[1]) {
            throw 'Temporary password confirmation did not match. No password was sent to the API and no database change was made.'
        }
        if ($encodedInputs[2] -cne $encodedInputs[3]) {
            throw 'Permanent password confirmation did not match. No password was sent to the API and no database change was made.'
        }
        if ($encodedInputs[0] -ceq $encodedInputs[2]) {
            throw 'The permanent password must differ from the temporary password. No password was sent to the API and no database change was made.'
        }
        return $encodedInputs
    }
    catch {
        [Array]::Clear($encodedInputs, 0, $encodedInputs.Length)
        throw
    }
}

function Invoke-DevelopmentAdminPasswordRecovery {
    [CmdletBinding()]
    param()

    $ErrorActionPreference = 'Stop'
    $process = $null
    $stderrTask = $null
    $paths = $null
    $runtimeDirectoryCreated = $false
    $secureInputs = [System.Collections.Generic.List[System.Security.SecureString]]::new()
    $encodedInputs = $null
    $passwordInputSent = $false

    try {
        $paths = Get-DevelopmentAdminRecoveryPaths -ScriptRoot $script:DevelopmentAdminRecoveryScriptRoot
        if (-not (Test-Path -LiteralPath $paths.ApiProject -PathType Leaf)) {
            throw 'The FOPX PM API project was not found. Run this procedure from the checked-out repository.'
        }

        $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
        if ($null -eq $dotnet) {
            throw 'The .NET SDK was not found on PATH. Install the repository-required .NET SDK and retry.'
        }

        if (Test-Path -LiteralPath $paths.ApiRuntimeDirectory) {
            throw 'The isolated Development recovery runtime directory already exists; no output was overwritten.'
        }
        $runtimeDirectoryCreated = $true
        Write-Host 'Building and validating a fresh isolated Development API runtime package.'
        $publishResult = Invoke-DevelopmentAdminRecoveryRuntimePublish -DotnetPath $dotnet.Source -ApiProject $paths.ApiProject -ApiProjectDirectory $paths.ApiProjectDirectory -OutputDirectory $paths.ApiRuntimeDirectory
        $runtimeVerification = Assert-DevelopmentAdminRecoveryRuntimeComplete -RuntimeDirectory $paths.ApiRuntimeDirectory
        Write-Host ("Verified {0} runtime dependency assets, including MppAdapter." -f $runtimeVerification.RuntimeAssetCount)

        # Windows Integrated Security uses the Product Owner's current Windows identity.
        # The API independently verifies SERVERPROPERTY('ServerName'), DB_NAME(), and DB_ID().
        $connectionBuilder = [System.Data.Common.DbConnectionStringBuilder]::new()
        $connectionBuilder['Server'] = 'Shady'
        $connectionBuilder['Database'] = 'NexusPmo'
        $connectionBuilder['Integrated Security'] = $true
        $connectionBuilder['Encrypt'] = $true
        $connectionBuilder['TrustServerCertificate'] = $true
        $connectionBuilder['MultipleActiveResultSets'] = $true

        $startInfo = New-DevelopmentAdminRecoveryStartInfo -DotnetPath $dotnet.Source -ApiRuntimeDirectory $paths.ApiRuntimeDirectory -ApiAssembly $paths.ApiAssembly -ConnectionString $connectionBuilder.ConnectionString
        $process = [System.Diagnostics.Process]::new()
        $process.StartInfo = $startInfo
        if (-not $process.Start()) {
            throw 'The Development Admin recovery process did not start.'
        }
        # Drain stderr concurrently so it cannot block the child while stdout is read.
        $stderrTask = $process.StandardError.ReadToEndAsync()

        Write-Host 'Checking Development environment, SQL Server/database identity, and migration readiness.'
        $inputReady = $false
        $timedOut = $false
        $preflightDeadline = [DateTime]::UtcNow.AddMinutes(2)
        while (-not $inputReady) {
            $readTask = $process.StandardOutput.ReadLineAsync()
            while (-not $readTask.Wait([TimeSpan]::FromSeconds(1))) {
                if ([DateTime]::UtcNow -ge $preflightDeadline) {
                    $timedOut = $true
                    if (-not $process.HasExited) {
                        $process.Kill($true)
                    }
                    break
                }
            }
            if ($timedOut) {
                break
            }

            $line = $readTask.GetAwaiter().GetResult()
            if ($null -eq $line) {
                break
            }
            if ($line -eq 'NEXUS_DEVELOPMENT_ADMIN_RECOVERY_INPUT_READY') {
                $inputReady = $true
                break
            }
            Write-DevelopmentAdminRecoveryDiagnostics -Text $line
        }

        if (-not $inputReady) {
            if (-not $process.HasExited) {
                $process.WaitForExit()
            }
            $exitCode = $process.ExitCode
            $stderrText = $stderrTask.GetAwaiter().GetResult()
            Write-DevelopmentAdminRecoveryDiagnostics -Text $stderrText
            $preflight = New-DevelopmentAdminRecoveryPreflightResult -InputReady $false -ExitCode $exitCode -TimedOut $timedOut
            throw $preflight.ErrorMessage
        }

        if ($process.HasExited) {
            $exitCode = $process.ExitCode
            $stderrText = $stderrTask.GetAwaiter().GetResult()
            Write-DevelopmentAdminRecoveryDiagnostics -Text $stderrText
            throw "Development Admin recovery child exited immediately after preflight (exit code: $exitCode); no password was requested or sent."
        }

        $preflight = New-DevelopmentAdminRecoveryPreflightResult -InputReady $true -ExitCode 0
        Write-Host 'Target verification passed. Enter the temporary and permanent passwords below; input will remain hidden.'
        $passwordInputs = Read-DevelopmentAdminRecoverySecureInputs -PreflightResult $preflight
        foreach ($secureValue in $passwordInputs) {
            $secureInputs.Add($secureValue)
        }
        $encodedInputs = ConvertTo-DevelopmentAdminRecoveryPipeLines -SecureInputs $passwordInputs

        foreach ($encodedLine in $encodedInputs) {
            $process.StandardInput.WriteLine($encodedLine)
        }
        $process.StandardInput.Close()
        $passwordInputSent = $true

        while (($line = $process.StandardOutput.ReadLine()) -ne $null) {
            Write-DevelopmentAdminRecoveryDiagnostics -Text $line
        }
        $process.WaitForExit()
        $exitCode = $process.ExitCode
        $stderrText = $stderrTask.GetAwaiter().GetResult()
        Write-DevelopmentAdminRecoveryDiagnostics -Text $stderrText

        if ($exitCode -ne 0) {
            throw "Development Admin recovery child exited with code $exitCode after secure input. Review the safe child diagnostics above. No password was printed or saved."
        }
        Write-Host 'Development Admin recovery completed successfully. The temporary login and permanent password flow were verified.'
    }
    catch {
        Write-Error ("Development Admin recovery stopped: {0}" -f $_.Exception.Message)
        throw
    }
    finally {
        foreach ($secureValue in $secureInputs) {
            if ($null -ne $secureValue) {
                $secureValue.Dispose()
            }
        }
        if ($encodedInputs) {
            [Array]::Clear($encodedInputs, 0, $encodedInputs.Length)
        }
        if ($process) {
            if (-not $process.HasExited) {
                try {
                    if (-not $passwordInputSent) {
                        $process.Kill($true)
                    }
                    $process.WaitForExit()
                }
                catch {
                    # Only the child launched by this procedure is eligible for cleanup.
                }
            }
            $process.Dispose()
        }
        if ($runtimeDirectoryCreated -and $paths) {
            Remove-DevelopmentAdminRecoveryRuntime -RuntimeDirectory $paths.ApiRuntimeDirectory
        }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    Invoke-DevelopmentAdminPasswordRecovery
}

