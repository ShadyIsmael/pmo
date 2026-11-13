# PowerShell script to extract permission catalog data
$basePath = "C:\Projects\pmo"
$content = Get-Content -Path "$basePath\apps\api\src\NEXUS.Modules.Identity\Application\PermissionCatalog.cs" -Raw

# Find the Catalog array content
$startMarker = 'private static readonly IReadOnlyList<PermissionCatalogEntry> Catalog ='
$start = $content.IndexOf($startMarker)
if ($start -eq -1) {
    $start = $content.IndexOf('private static readonly IReadOnlyList<PermissionCatalogEntry> Catalog')
}

# Find the opening bracket
$catalogStart = $content.IndexOf('[', $start)
$bracketCount = 0
$catalogEnd = $catalogStart
for ($i = $catalogStart; $i -lt $content.Length; $i++) {
    $char = $content[$i]
    if ($char -eq '[') { $bracketCount++ }
    elseif ($char -eq ']') { 
        $bracketCount--
        if ($bracketCount -eq 0) { $catalogEnd = $i + 1; break }
    }
}

$catalogContent = $content.Substring($catalogStart, $catalogEnd - $catalogStart)

# Extract all SeedIds to count entries
$seedIds = [regex]::Matches($catalogContent, '"(b000\d{12})"')
Write-Host "Seed IDs found: $($seedIds.Count)"

# Try to extract each entry using regex
$entryPattern = 'new\s*\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*PermissionLevel\.(\w+)\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*(true|false)\s*,\s*\[([^\]]*)\]\s*,\s*(true|false)(?:\s*,\s*PermissionScope\.(\w+))?(?:\s*,\s*IsAssignable:\s*(true|false))?(?:\s*,\s*IsTenantAdminBaseline:\s*(true|false))?\s*\)'

$matches = [regex]::Matches($catalogContent, $entryPattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
Write-Host "Entries matched: $($matches.Count)"

# Write CSV
$csvPath = "$basePath\docs\security\permission-audit\PERMISSION-CATALOG-INVENTORY.csv"
$csvLines = @()
$csvLines += "SeedId,CanonicalKey,Resource,Action,ModuleId,ModuleNameEn,ModuleNameAr,ScreenId,ScreenNameEn,ScreenNameAr,PermissionLevel,LabelEn,LabelAr,DescriptionEn,DescriptionAr,Sensitivity,IsSensitive,RequiredPermissionKeys,IsActive,Scope,IsAssignable,IsTenantAdminBaseline,SourceFile,SourceLine"

$lineNum = 27
foreach ($match in $matches) {
    $groups = $match.Groups
    $seedId = $groups[1].Value
    $key = $groups[2].Value
    $resource = $groups[3].Value
    $action = $groups[4].Value
    $moduleId = $groups[5].Value
    $moduleNameEn = $groups[6].Value
    $moduleNameAr = $groups[7].Value
    $screenId = $groups[8].Value
    $screenNameEn = $groups[9].Value
    $screenNameAr = $groups[10].Value
    $permLevel = $groups[11].Value
    $labelEn = $groups[12].Value
    $labelAr = $groups[13].Value
    $descEn = $groups[14].Value
    $descAr = $groups[15].Value
    $sensitivity = $groups[16].Value
    $isSensitive = $groups[17].Value
    $reqKeysStr = $groups[18].Value
    $isActive = $groups[19].Value
    $scope = if ($groups[20].Success) { $groups[20].Value } else { "Tenant" }
    $isAssignable = if ($groups[21].Success) { $groups[21].Value } else { "true" }
    $isBaseline = if ($groups[22].Success) { $groups[22].Value } else { "false" }
    
    # Parse required keys
    $reqKeys = @()
    if ($reqKeysStr.Trim()) {
        $reqKeys = $reqKeysStr.Split(',') | ForEach-Object { $_.Trim().Trim('"') } | Where-Object { $_ }
    }
    
    $line = @(
        $seedId, $key, $resource, $action, $moduleId, $moduleNameEn, $moduleNameAr,
        $screenId, $screenNameEn, $screenNameAr, $permLevel, $labelEn, $labelAr,
        $descEn, $descAr, $sensitivity, $isSensitive, ($reqKeys -join ';'),
        $isActive, $scope, $isAssignable, $isBaseline,
        'apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs', "Line ~$lineNum"
    ) | ForEach-Object { '"' + $_.Replace('"', '""') + '"' } | Join-String -Separator ','
    
    $csvLines += $line
    $lineNum += 12
}

$csvLines | Set-Content -Path $csvPath -Encoding UTF8
Write-Host "CSV written to $csvPath"

# Summary
$activeCount = ($matches | Where-Object { $_.Groups[19].Value -eq 'true' }).Count
$inactiveCount = $matches.Count - $activeCount
$viewCount = ($matches | Where-Object { $_.Groups[11].Value -eq 'View' }).Count
$actionCount = ($matches | Where-Object { $_.Groups[11].Value -eq 'Action' }).Count

Write-Host "`nSummary:"
Write-Host "Total entries: $($matches.Count)"
Write-Host "Active: $activeCount"
Write-Host "Inactive: $inactiveCount"
Write-Host "View: $viewCount"
Write-Host "Action: $actionCount"

# Module breakdown
$modules = @{}
foreach ($match in $matches) {
    $mod = $match.Groups[5].Value
    if (-not $modules.ContainsKey($mod)) { $modules[$mod] = 0 }
    $modules[$mod]++
}
Write-Host "`nModule breakdown:"
$modules.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Host "  $($_.Name): $($_.Value)" }

# Scope breakdown
$scopes = @{}
foreach ($match in $matches) {
    $scope = if ($match.Groups[20].Success) { $match.Groups[20].Value } else { "Tenant" }
    if (-not $scopes.ContainsKey($scope)) { $scopes[$scope] = 0 }
    $scopes[$scope]++
}
Write-Host "`nScope breakdown:"
$scopes.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Host "  $($_.Name): $($_.Value)" }

# Baseline count
$baselineCount = ($matches | Where-Object { $_.Groups[22].Success -and $_.Groups[22].Value -eq 'true' }).Count
Write-Host "`nIsTenantAdminBaseline: $baselineCount"

# Duplicate check
$keys = $matches | ForEach-Object { $_.Groups[2].Value }
$duplicates = $keys | Group-Object | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Name }
Write-Host "`nDuplicate canonical keys: $($duplicates.Count)"
foreach ($d in $duplicates) { Write-Host "  $d" }