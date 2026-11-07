<#
.SYNOPSIS
Drops the NexusPmo database and rebuilds it with an empty schema plus only identity data (users, roles, permissions).

.DESCRIPTION
This script performs the following steps:
  1. Drops the existing NexusPmo database (force-closing active connections).
  2. Creates a new empty NexusPmo database.
  3. Applies all pending EF Core migrations.
  4. Seeds only identity tables (Users, Roles, Permissions, UserRoles, UserSessions).

.PARAMETER UseDockerSqlAuth
Set this switch if you want to connect to a Docker-hosted SQL Server using SQL authentication (sa).
The password is read from the NEXUS_DB_PASSWORD environment variable; no credential is stored in this script.
By default, Windows Authentication (Trusted_Connection) is used for local SQL Server instances.

.EXAMPLE
  .\reset-database-users-only.ps1

.EXAMPLE
  .\reset-database-users-only.ps1 -UseDockerSqlAuth
#>

param(
    [switch]$UseDockerSqlAuth
)

$ErrorActionPreference = "Stop"

$DatabaseName = "NexusPmo"
$ProjectPath = "C:\Projects\pmo\apps\api\src\NEXUS.Api\NEXUS.Api.csproj"
$Server = "localhost"

$dropSql = @"
IF EXISTS (SELECT name FROM sys.databases WHERE name = '$DatabaseName')
BEGIN
    ALTER DATABASE [$DatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$DatabaseName];
    PRINT 'Dropped database [$DatabaseName]';
END
ELSE
BEGIN
    PRINT 'Database [$DatabaseName] does not exist.';
END
"@

$createSql = "CREATE DATABASE [$DatabaseName];"

if ($UseDockerSqlAuth) {
    $SqlUser = "sa"
    $SqlPassword = $env:NEXUS_DB_PASSWORD

    if ([string]::IsNullOrWhiteSpace($SqlPassword)) {
        throw "NEXUS_DB_PASSWORD must be set to use SQL authentication. Set it in the environment or the repository .env file."
    }
    
    Write-Host "=== Dropping database '$DatabaseName' if it exists ===" -ForegroundColor Cyan
    sqlcmd -S $Server -U $SqlUser -P $SqlPassword -b -Q $dropSql

    Write-Host "=== Creating database '$DatabaseName' ===" -ForegroundColor Cyan
    sqlcmd -S $Server -U $SqlUser -P $SqlPassword -b -Q $createSql
} else {
    Write-Host "=== Dropping database '$DatabaseName' if it exists ===" -ForegroundColor Cyan
    sqlcmd -S $Server -E -b -Q $dropSql

    Write-Host "=== Creating database '$DatabaseName' ===" -ForegroundColor Cyan
    sqlcmd -S $Server -E -b -Q $createSql
}

Write-Host "=== Applying EF Core migrations ===" -ForegroundColor Cyan
dotnet ef database update --project $ProjectPath --no-build

if ($LASTEXITCODE -ne 0) {
    Write-Host "dotnet ef database update failed. Trying without --no-build..." -ForegroundColor Yellow
    dotnet ef database update --project $ProjectPath
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to apply EF Core migrations."
    }
}

Write-Host "=== Seeding identity data only ===" -ForegroundColor Cyan
dotnet run --project $ProjectPath -- --seed-identity

Write-Host "=== Database rebuild complete. Only identity data was seeded. ===" -ForegroundColor Green
