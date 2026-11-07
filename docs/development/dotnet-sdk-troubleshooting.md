# .NET SDK and API Build Troubleshooting

**Repository:** C:/Projects/pmo  
**Rechecked:** 2026-11-01

## Repository SDK selection

- global.json pins .NET SDK 10.0.302 with rollForward set to latestFeature. The only SDK visible to where.exe dotnet and dotnet --list-sdks in the restricted execution environment was 10.0.302.
- The API targets net10.0 and uses the standard Microsoft.NET.Sdk project SDK. No tracked Directory.Build.props or Directory.Build.targets was found.
- The x64 .NET runtime list included ASP.NET Core and .NET 8.0.31 and 10.0.10.

Do not change global.json, target frameworks, or system PATH as a workaround for an incomplete installation.

## Verified environment findings

- The SDK directory C:/Program Files/dotnet/sdk/10.0.302/Sdks does not contain Microsoft.NET.Sdk.WorkloadAutoImportPropsLocator or Microsoft.NET.Sdk.WorkloadManifestTargetsLocator.
- SdkResolvers/Microsoft.NET.Sdk.WorkloadMSBuildSdkResolver contains only its XML manifest, not the resolver binaries expected from a complete installation.
- dotnet --info and dotnet workload list report that the process cannot open the Windows Service Control Manager because access is denied. This is an environment privilege issue; it is not itself a compiler diagnostic.
- vswhere reported SQL Server Management Studio 22.8.1 only. No Visual Studio or MSBuild executable was found on PATH.
- An exact-version Microsoft installer download previously failed because DNS resolution was unavailable. No machine SDK repair, PATH edit, file copy, or framework change was performed.

## Current restore and build results

- dotnet restore apps/api/src/NEXUS.Api/NEXUS.Api.csproj exited 1. Diagnostic output showed the restore project-walk targets _GetAllRestoreProjectPathItems and _GenerateRestoreProjectPathWalk failing, but no MSBuild or NuGet error code was surfaced in this recheck. An earlier execution recorded MSB4276 while resolving the missing workload resolver SDK paths.
- dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore -m:1 --verbosity minimal compiled the project references through NEXUS.Persistence, then failed with MSB4018 in NEXUS.Modules.ProjectPlanning.MppAdapter. IKVM could not open obj/Debug/net10.0/ikvm/stage/1/1ef14f78975de40d85569d4de24e8418/jakarta.activation.dll.log because another process held the file. No dotnet, MSBuild, or Java process was visible immediately after the failure, so the lock owner is unknown.

This is not a successful API build. The missing SDK components and the IKVM file lock are separate findings; repairing the SDK alone does not prove the IKVM lock is resolved.

## Host-side SDK repair

Use the official Microsoft Windows installer for the same x64 SDK version pinned by the repository. Verify the installer against the SHA-512 published by Microsoft. From an elevated PowerShell session in the directory containing the installer, run:

    .\dotnet-sdk-10.0.302-win-x64.exe /repair

Microsoft documents the /repair installer option as restoring damaged .NET installation files and components. If repair cannot restore the missing resolver files, reinstall the same pinned SDK using its official installer. Do not copy resolver binaries from a different SDK installation.

After repair, verify the selected SDK and workload tooling:

    where.exe dotnet
    dotnet --info
    dotnet --list-sdks
    dotnet --list-runtimes
    dotnet workload list
    Test-Path 'C:\Program Files\dotnet\sdk\10.0.302\Sdks\Microsoft.NET.Sdk.WorkloadAutoImportPropsLocator'
    Test-Path 'C:\Program Files\dotnet\sdk\10.0.302\Sdks\Microsoft.NET.Sdk.WorkloadManifestTargetsLocator'

Both Test-Path checks should return True after a successful repair.

## Resolve the IKVM file lock and verify the API

Before retrying the full build, use Windows Resource Monitor's CPU > Associated Handles search to identify the process holding jakarta.activation.dll.log. Confirm the process owner and allow its active build or file operation to finish. Do not delete obj files or terminate an unknown process. Run the API build serially after the handle is released.

    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    dotnet restore .\apps\api\src\NEXUS.Api\NEXUS.Api.csproj
    dotnet build .\apps\api\src\NEXUS.Api\NEXUS.Api.csproj --no-restore -m:1 --verbosity minimal

DOTNET_CLI_TELEMETRY_OPTOUT controls .NET CLI telemetry only. It does not disable Application Insights SDK telemetry in an application, test, or development tool. Do not treat it as approval to bypass a separate egress review.

## Microsoft references

- [Install .NET on Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows) — supported installer options, including /repair.
- [.NET 10 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) — official SDK installers and published integrity information.
