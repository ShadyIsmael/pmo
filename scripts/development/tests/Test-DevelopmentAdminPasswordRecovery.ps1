#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$scriptPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Recover-DevelopmentAdminPassword.ps1'))
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$originalDirectory = (Get-Location).Path
$originalPath = $env:PATH
$originalTelemetryOptOut = $env:DOTNET_CLI_TELEMETRY_OPTOUT
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$testRoot = Join-Path $env:TEMP ('FopxAdminRecoveryPreflight-' + [Guid]::NewGuid().ToString('N'))
$testCount = 0
$global:FopxAdminRecoveryMockReadCount = 0
$global:FopxAdminRecoveryMockReadIndex = 0
$global:FopxAdminRecoveryMockPasswordValues = @(
    'MockTemporary!12345',
    'MockTemporary!12345',
    'MockPermanent$54321',
    'MockPermanent$54321'
)

function Assert-RecoveryTest {
    param([bool] $Condition, [string] $Message)
    if (-not $Condition) {
        throw "FAIL: $Message"
    }
    $script:testCount++
    Write-Output "PASS: $Message"
}

function Read-Host {
    [CmdletBinding()]
    param(
        [Parameter(Position = 0)][string] $Prompt,
        [switch] $AsSecureString
    )

    if (-not $AsSecureString) {
        throw 'The recovery procedure requested non-secure input.'
    }
    $global:FopxAdminRecoveryMockReadCount++
    if ($global:FopxAdminRecoveryMockReadIndex -ge $global:FopxAdminRecoveryMockPasswordValues.Count) {
        throw 'The recovery procedure requested too many password entries.'
    }
    $value = $global:FopxAdminRecoveryMockPasswordValues[$global:FopxAdminRecoveryMockReadIndex]
    $global:FopxAdminRecoveryMockReadIndex++
    ConvertTo-SecureString -String $value -AsPlainText -Force
}

function New-MockRecoveryHost {
    param([Parameter(Mandatory)][string] $Mode)

    $modeRoot = Join-Path $testRoot $Mode
    $projectPath = Join-Path $modeRoot 'MockRecoveryHost.csproj'
    $sourcePath = Join-Path $modeRoot 'Program.cs'
    $outputPath = Join-Path $modeRoot 'bin'
    New-Item -ItemType Directory -Force -Path $modeRoot | Out-Null

    $project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>dotnet</AssemblyName>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
'@
    $source = @'
using System;
using System.IO;
using System.Linq;
using System.Text;

if (args.Length > 0 && args[0] == "publish")
{
    var outputIndex = Array.IndexOf(args, "--output");
    if (outputIndex < 0 || outputIndex + 1 >= args.Length)
        return 60;

    var outputDirectory = args[outputIndex + 1];
    var buildPropertiesArgument = args.FirstOrDefault(value => value.StartsWith("-p:CustomAfterMicrosoftCommonProps=", StringComparison.OrdinalIgnoreCase));
    var buildPropertiesPath = buildPropertiesArgument is null ? "" : buildPropertiesArgument["-p:CustomAfterMicrosoftCommonProps=".Length..];
    var buildProperties = File.Exists(buildPropertiesPath) ? File.ReadAllText(buildPropertiesPath) : "";
    var expectedBuildOutputDirectory = Path.GetFullPath(Path.Combine(outputDirectory, "build")) + Path.DirectorySeparatorChar;
    var isolatedBuildOutputOk = buildProperties.Contains("Condition=\"'$(MSBuildProjectName)' == 'NEXUS.Api'\"", StringComparison.Ordinal)
        && buildProperties.Contains("<OutputPath>" + expectedBuildOutputDirectory + "</OutputPath>", StringComparison.Ordinal)
        && !args.Any(value => value.StartsWith("-p:OutputPath=", StringComparison.OrdinalIgnoreCase));
    Console.WriteLine("MOCK_ISOLATED_BUILD_OUTPUT_OK=" + isolatedBuildOutputOk);
    Directory.CreateDirectory(outputDirectory);
    var runtimeFiles = new[]
    {
        "NEXUS.Api.dll",
        "NEXUS.Modules.ProjectPlanning.MppAdapter.dll",
        "NEXUS.Modules.ProjectPlanning.dll",
        "MPXJ.Net.dll",
        "mpxj.dll",
        "IKVM.ByteCode.dll",
        "System.Drawing.Common.dll",
        "System.Private.Windows.Core.dll",
        "System.Private.Windows.GdiPlus.dll"
    };
    foreach (var fileName in runtimeFiles)
        File.WriteAllText(Path.Combine(outputDirectory, fileName), "mock-runtime-asset");
    Directory.CreateDirectory(Path.Combine(outputDirectory, "fr"));
    File.WriteAllText(Path.Combine(outputDirectory, "fr", "Microsoft.Data.SqlClient.resources.dll"), "mock-satellite-asset");

    var dependencies = """
    {
      "runtimeTarget": { "name": ".NETCoreApp,Version=v10.0" },
      "targets": {
        ".NETCoreApp,Version=v10.0": {
          "NEXUS.Api/1.0.0": { "runtime": { "NEXUS.Api.dll": {} } },
          "NEXUS.Modules.ProjectPlanning.MppAdapter/1.0.0": { "runtime": { "NEXUS.Modules.ProjectPlanning.MppAdapter.dll": {} } },
          "NEXUS.Modules.ProjectPlanning/1.0.0": { "runtime": { "NEXUS.Modules.ProjectPlanning.dll": {} } },
          "MPXJ.Net/16.10.0": { "runtime": { "MPXJ.Net.dll": {} } },
          "mpxj/16.10.0": { "runtime": { "mpxj.dll": {} } },
          "IKVM.ByteCode/9.3.4": { "runtime": { "IKVM.ByteCode.dll": {} } },
          "System.Drawing.Common/10.0.0": { "runtime": { "System.Drawing.Common.dll": {}, "System.Private.Windows.Core.dll": {}, "System.Private.Windows.GdiPlus.dll": {} } },
          "Microsoft.Data.SqlClient/6.1.1": { "resources": { "lib/net9.0/fr/Microsoft.Data.SqlClient.resources.dll": {} } }
        }
      },
      "libraries": {}
    }
    """;
    File.WriteAllText(Path.Combine(outputDirectory, "NEXUS.Api.deps.json"), dependencies);
    File.WriteAllText(Path.Combine(outputDirectory, "NEXUS.Api.runtimeconfig.json"), "{}");
    Console.WriteLine("MOCK_PUBLISH_OK=True");
    return 0;
}

var expectedAssembly = args.Length > 0 && args[0].EndsWith("NEXUS.Api.dll", StringComparison.OrdinalIgnoreCase);
var expectedArguments = args.Length == 6
    && args[1] == "--recover-development-admin-password"
    && args[2] == "--admin-recovery-expected-sql-server"
    && args[3] == "Shady"
    && args[4] == "--admin-recovery-expected-sql-database"
    && args[5] == "NexusPmo";
var environmentOk =
    string.Equals(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase)
    && string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
var connection = Environment.GetEnvironmentVariable("NEXUS_EF_DEVELOPMENT_CONNECTION_STRING") ?? "";
var connectionOk = connection.Contains("Shady", StringComparison.OrdinalIgnoreCase)
    && connection.Contains("NexusPmo", StringComparison.OrdinalIgnoreCase)
    && connection.Contains("Integrated Security=True", StringComparison.OrdinalIgnoreCase);
var workingDirectoryOk = string.Equals(
    Path.GetFullPath(Environment.CurrentDirectory),
    Path.GetDirectoryName(Path.GetFullPath(args[0])),
    StringComparison.OrdinalIgnoreCase);

Console.WriteLine("MOCK_DIRECT_DLL_OK=" + (expectedAssembly && expectedArguments));
Console.WriteLine("MOCK_ENVIRONMENT_OK=" + environmentOk);
Console.WriteLine("MOCK_CONNECTION_TARGET_OK=" + connectionOk);
Console.WriteLine("MOCK_PROJECT_WORKING_DIRECTORY_OK=" + workingDirectoryOk);

#if MOCK_FAILURE
Console.Error.WriteLine("MOCK_SAFE_FAILURE: simulated preflight failure; Server=MockSql;Database=MockDb;Password=MockSecretForRedaction");
return 37;
#else
Console.WriteLine("NEXUS_DEVELOPMENT_ADMIN_RECOVERY_INPUT_READY");
var received = new string[4];
for (var index = 0; index < received.Length; index++)
{
    var line = Console.ReadLine();
    if (line is null)
        return 51;
    try
    {
        _ = Convert.FromBase64String(line);
        received[index] = line;
    }
    catch (FormatException)
    {
        return 52;
    }
}
if (Console.ReadLine() is not null)
    return 53;
Console.WriteLine("MOCK_SECURE_INPUT_LINES=4");
return 0;
#endif
'@

    Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8
    Set-Content -LiteralPath $sourcePath -Value $source -Encoding utf8
    $dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source
    $buildArguments = @('build', $projectPath, '--configuration', 'Debug', '--output', $outputPath, '--nologo', '--verbosity', 'quiet', '-p:NuGetAudit=false')
    if ($Mode -eq 'Failure') {
        $buildArguments += '-p:DefineConstants=MOCK_FAILURE'
    }
    & $dotnetPath @buildArguments *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Mock recovery host build failed with exit code $LASTEXITCODE."
    }
    return $outputPath
}

function Invoke-RecoveryScriptAgainstMock {
    param([Parameter(Mandatory)][string] $MockHostDirectory)

    $savedPath = $env:PATH
    $savedLocation = (Get-Location).Path
    $global:FopxAdminRecoveryMockReadCount = 0
    $global:FopxAdminRecoveryMockReadIndex = 0
    $env:PATH = $MockHostDirectory + ';' + $savedPath
    $output = [System.Collections.Generic.List[string]]::new()
    $caughtMessage = $null
    try {
        Set-Location -LiteralPath (Join-Path $env:SystemRoot 'System32')
        & $scriptPath *>&1 | ForEach-Object { $output.Add($_.ToString()) }
    }
    catch {
        $caughtMessage = $_.Exception.Message
    }
    finally {
        Set-Location -LiteralPath $savedLocation
        $env:PATH = $savedPath
    }
    [pscustomobject]@{
        Output = ($output -join [Environment]::NewLine) + [Environment]::NewLine + [string]$caughtMessage
        Error = $caughtMessage
        ReadCount = $global:FopxAdminRecoveryMockReadCount
    }
}

try {
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        throw "Recovery script not found at its absolute path: $scriptPath"
    }

    $tokens = $null
    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors) | Out-Null
    Assert-RecoveryTest ($parseErrors.Count -eq 0) 'PowerShell recovery script parses'

    Set-Location -LiteralPath (Join-Path $env:SystemRoot 'System32')
    . $scriptPath
    $paths = Get-DevelopmentAdminRecoveryPaths -ScriptRoot (Split-Path -Parent $scriptPath)
    Assert-RecoveryTest ([System.IO.Path]::IsPathRooted($paths.RepositoryRoot) -and $paths.RepositoryRoot -eq $repoRoot) 'absolute script path resolves repository from System32'
    Assert-RecoveryTest ([System.IO.Path]::IsPathRooted($paths.ApiRuntimeDirectory) -and [System.IO.Path]::IsPathRooted($paths.ApiAssembly)) 'isolated runtime output and API assembly paths are absolute'
    Assert-RecoveryTest ($paths.ApiRuntimeDirectory -notmatch '\\bin\\Debug\\net10\.0' -and $paths.ApiAssembly -eq (Join-Path $paths.ApiRuntimeDirectory 'NEXUS.Api.dll')) 'recovery runtime does not select the incomplete shared Debug output'

    $testConnection = 'Server=Shady;Database=NexusPmo;Integrated Security=True'
    $startInfo = New-DevelopmentAdminRecoveryStartInfo -DotnetPath 'C:\Mock\dotnet.exe' -ApiRuntimeDirectory $paths.ApiRuntimeDirectory -ApiAssembly $paths.ApiAssembly -ConnectionString $testConnection
    $arguments = @($startInfo.ArgumentList)
    Assert-RecoveryTest ($arguments[0] -eq $paths.ApiAssembly -and $arguments -notcontains 'run' -and $arguments -notcontains '--launch-profile') 'child invokes the built API DLL directly without dotnet run or launch profiles'
    Assert-RecoveryTest ($startInfo.WorkingDirectory -eq $paths.ApiRuntimeDirectory) 'child working directory is the exact published runtime directory'
    Assert-RecoveryTest ($startInfo.Environment['ASPNETCORE_ENVIRONMENT'] -eq 'Development' -and $startInfo.Environment['DOTNET_ENVIRONMENT'] -eq 'Development') 'Development environment is set explicitly for the child'
    Assert-RecoveryTest ($startInfo.RedirectStandardError -and $startInfo.RedirectStandardOutput -and $startInfo.RedirectStandardInput) 'child stdout, stderr, and stdin are redirected for controlled diagnostics and secure input'

    $successResult = New-DevelopmentAdminRecoveryPreflightResult -InputReady $true -ExitCode 0
    $failureResult = New-DevelopmentAdminRecoveryPreflightResult -InputReady $false -ExitCode 37
    Assert-RecoveryTest $successResult.Succeeded 'mocked preflight success is accepted'
    Assert-RecoveryTest (-not $failureResult.Succeeded -and $failureResult.ErrorMessage.Contains('37')) 'mocked preflight failure preserves its child exit code'

    $unsafeLine = 'MOCK_SAFE_FAILURE: Server=MockSql;Database=MockDb;Password=MockSecretForRedaction'
    $safeLine = Protect-DevelopmentAdminRecoveryDiagnostic -Text $unsafeLine
    Assert-RecoveryTest (-not $safeLine.Contains('MockSecretForRedaction') -and -not $safeLine.Contains('Server=MockSql')) 'safe diagnostics redact connection details and credential fields'

    $readCountBeforeFailure = 0
    $neverRead = { param([string] $PromptText) $script:readCountBeforeFailure++; ConvertTo-SecureString 'MockOnlyPassword!123' -AsPlainText -Force }
    try {
        Read-DevelopmentAdminRecoverySecureInputs -PreflightResult $failureResult -SecureReader $neverRead | Out-Null
        throw 'Expected failed preflight to stop before password collection.'
    }
    catch {
        Assert-RecoveryTest ($_.Exception.Message.Contains('exit code: 37')) 'failed mocked preflight returns a safe actionable error'
    }
    Assert-RecoveryTest ($readCountBeforeFailure -eq 0) 'failed preflight does not call the secure password reader'

    $global:FopxAdminRecoveryCancelReadCount = 0
    $partialSecureInput = ConvertTo-SecureString 'MockPartialInput!123' -AsPlainText -Force
    $cancelReader = {
        param([string] $PromptText)
        $global:FopxAdminRecoveryCancelReadCount++
        if ($global:FopxAdminRecoveryCancelReadCount -eq 1) { return $partialSecureInput }
        return $null
    }
    try {
        Read-DevelopmentAdminRecoverySecureInputs -PreflightResult $successResult -SecureReader $cancelReader | Out-Null
        throw 'Expected canceled secure input to stop collection.'
    }
    catch {
        Assert-RecoveryTest ($_.Exception.Message.Contains('was canceled')) 'secure-input cancellation stops collection safely'
    }
    $partialWasDisposed = $false
    try { $partialSecureInput.AppendChar([char]'x') }
    catch { $partialWasDisposed = $_.Exception.InnerException -is [System.ObjectDisposedException] }
    Assert-RecoveryTest $partialWasDisposed 'partial secure input is disposed when password collection is canceled'

    $global:FopxAdminRecoverySuccessReaderCount = 0
    $successInputs = @(Read-DevelopmentAdminRecoverySecureInputs -PreflightResult $successResult -SecureReader {
        param([string] $PromptText)
        $global:FopxAdminRecoverySuccessReaderCount++
        ConvertTo-SecureString -String $global:FopxAdminRecoveryMockPasswordValues[$global:FopxAdminRecoverySuccessReaderCount - 1] -AsPlainText -Force
    })
    $encoded = ConvertTo-DevelopmentAdminRecoveryPipeLines -SecureInputs $successInputs
    Assert-RecoveryTest ($successInputs.Count -eq 4 -and $global:FopxAdminRecoverySuccessReaderCount -eq 4 -and $encoded.Count -eq 4) 'successful mocked preflight collects and encodes all four confirmed secure inputs'
    Assert-RecoveryTest (($encoded -join '') -notmatch 'MockTemporary|MockPermanent') 'secure input transport does not contain plaintext values'
    [Array]::Clear($encoded, 0, $encoded.Length)
    foreach ($secureValue in $successInputs) { $secureValue.Dispose() }

    $successHost = New-MockRecoveryHost -Mode Success
    $failureHost = New-MockRecoveryHost -Mode Failure

    $publishCheckDirectory = Join-Path $testRoot 'package-validation'
    $publishCheck = Invoke-DevelopmentAdminRecoveryRuntimePublish -DotnetPath (Join-Path $successHost 'dotnet.exe') -ApiProject $paths.ApiProject -ApiProjectDirectory $paths.ApiProjectDirectory -OutputDirectory $publishCheckDirectory
    $publishCheckOutput = $publishCheck.Output -join [Environment]::NewLine
    $runtimeCheck = Assert-DevelopmentAdminRecoveryRuntimeComplete -RuntimeDirectory $publishCheckDirectory
    Assert-RecoveryTest ($publishCheck.Arguments -contains '-p:BuildProjectReferences=false' -and $publishCheck.Arguments -contains ("-p:CustomAfterMicrosoftCommonProps=" + $publishCheck.BuildPropertiesPath)) 'publish disables project-reference rebuilds and directs only NEXUS.Api build output into the isolated runtime directory'
    Assert-RecoveryTest ($publishCheckOutput.Contains('MOCK_ISOLATED_BUILD_OUTPUT_OK=True')) 'mock build confirms API output avoids shared bin directories that a running API may have loaded'
    Assert-RecoveryTest ($runtimeCheck.RuntimeAssetCount -eq 10 -and (Test-Path -LiteralPath $runtimeCheck.MppAdapterAssembly -PathType Leaf)) 'complete runtime validation resolves API, project planning, MppAdapter, MPXJ, IKVM, System.Drawing, and localized resources'

    $failureRun = Invoke-RecoveryScriptAgainstMock -MockHostDirectory $failureHost
    Assert-RecoveryTest ($failureRun.ReadCount -eq 0) 'absolute script invocation from System32 collects no password when mocked preflight fails'
    Assert-RecoveryTest ($failureRun.Output.Contains('exit code: 37') -and $failureRun.Output.Contains('MOCK_SAFE_FAILURE')) 'failed child exit code and safe stderr are surfaced'
    Assert-RecoveryTest (-not $failureRun.Output.Contains('MockSecretForRedaction') -and -not $failureRun.Output.Contains('Server=MockSql')) 'preflight failure output does not reveal connection details or credential fields'
    Assert-RecoveryTest (-not $failureRun.Output.Contains('Using launch settings')) 'recovery invocation does not load launchSettings.json'
    Assert-RecoveryTest ($failureRun.Output.Contains('MOCK_ENVIRONMENT_OK=True') -and $failureRun.Output.Contains('MOCK_CONNECTION_TARGET_OK=True') -and $failureRun.Output.Contains('MOCK_DIRECT_DLL_OK=True') -and $failureRun.Output.Contains('MOCK_PROJECT_WORKING_DIRECTORY_OK=True')) 'mock child confirms Development environment, target configuration, direct DLL invocation, and project working directory'
    Assert-RecoveryTest ($failureRun.Output.Contains('Verified 10 runtime dependency assets, including MppAdapter.')) 'mock publish validates API, project planning, MppAdapter, MPXJ, IKVM, System.Drawing, and localized resource assets before preflight'

    $successRun = Invoke-RecoveryScriptAgainstMock -MockHostDirectory $successHost
    Assert-RecoveryTest ($successRun.Error -eq $null -and $successRun.ReadCount -eq 4) 'successful mocked preflight collects exactly four hidden password inputs'
    Assert-RecoveryTest ($successRun.Output.Contains('MOCK_SECURE_INPUT_LINES=4') -and $successRun.Output.Contains('recovery completed successfully')) 'secure stdin flow runs after successful mocked preflight'
    Assert-RecoveryTest (-not $successRun.Output.Contains('MockTemporary!12345') -and -not $successRun.Output.Contains('MockPermanent$54321')) 'successful mock run does not print secure input values'
    Assert-RecoveryTest ($successRun.Output.Contains('Verified 10 runtime dependency assets, including MppAdapter.')) 'successful mocked preflight uses a fully validated isolated runtime package'
    Assert-RecoveryTest (-not $successRun.Output.Contains('Using launch settings')) 'successful recovery also bypasses launch-profile output'

    Write-Output "Focused PowerShell recovery tests: $testCount passed, 0 failed."
}
finally {
    Set-Location -LiteralPath $originalDirectory
    $env:PATH = $originalPath
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = $originalTelemetryOptOut
    Remove-Variable -Scope Global -Name FopxAdminRecoveryMockReadCount,FopxAdminRecoveryMockReadIndex,FopxAdminRecoveryMockPasswordValues,FopxAdminRecoverySuccessReaderCount,FopxAdminRecoveryCancelReadCount -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
