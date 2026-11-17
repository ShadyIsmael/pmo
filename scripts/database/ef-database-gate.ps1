[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('model', 'inspect', 'script', 'apply', 'reset', 'sync-project-plan-permissions')]
    [string] $Action,

    [switch] $NoBuild,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $GateArguments
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($env:ASPNETCORE_ENVIRONMENT)) {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
}
elseif ($env:ASPNETCORE_ENVIRONMENT -cne 'Development') {
    throw 'EF database gate requires ASPNETCORE_ENVIRONMENT=Development.'
}

if ([string]::IsNullOrWhiteSpace($env:DOTNET_ENVIRONMENT)) {
    $env:DOTNET_ENVIRONMENT = 'Development'
}
elseif ($env:DOTNET_ENVIRONMENT -cne 'Development') {
    throw 'EF database gate requires DOTNET_ENVIRONMENT=Development.'
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$projectPath = Join-Path $repositoryRoot 'apps\api\tools\NEXUS.EFDatabaseGate\NEXUS.EFDatabaseGate.csproj'

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "EF database gate project not found: $projectPath"
}

Push-Location $repositoryRoot
try {
    if ($NoBuild) {
        $builtAssembly = Join-Path (Split-Path -Parent $projectPath) 'bin\Debug\net10.0\NEXUS.EFDatabaseGate.dll'
        if (-not (Test-Path -LiteralPath $builtAssembly -PathType Leaf)) {
            throw "No-build requested but the EF gate output is missing: $builtAssembly"
        }
    }
    else {
        & dotnet restore $projectPath --ignore-failed-sources --verbosity minimal --disable-parallel -m:1
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }

        & dotnet build $projectPath --no-restore --verbosity minimal -m:1
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

    & dotnet run --no-build --project $projectPath -- $Action @GateArguments
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
