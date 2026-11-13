$content = Get-Content -Path 'C:\Projects\pmo\apps\api\src\NEXUS.Modules.Identity\Application\PermissionCatalog.cs' -Raw -Encoding UTF8
$startMarker = 'private static readonly IReadOnlyList<PermissionCatalogEntry> Catalog ='
$start = $content.IndexOf($startMarker)
if ($start -eq -1) { $start = $content.IndexOf('private static readonly IReadOnlyList<PermissionCatalogEntry> Catalog') }

$catalogStart = $content.IndexOf('[', $start)
$bracketCount = 0
$catalogEnd = $catalogStart
for ($i = $catalogStart; $i -lt $content.Length; $i++) {
    $char = $content[$i]
    if ($char -eq '[') { $bracketCount++ }
    elseif ($char -eq ']') { $bracketCount--; if ($bracketCount -eq 0) { $catalogEnd = $i + 1; break } }
}

$catalogContent = $content.Substring($catalogStart, $catalogEnd - $catalogStart)

# Split by 'new(' to get individual entries
$entries = $catalogContent.Split([string[]]@('new('), [StringSplitOptions]::RemoveEmptyEntries)
Write-Host "Raw entry count: $($entries.Count)"

# Parse each entry
$csvPath = "C:\Projects\pmo\docs\security\permission-audit\PERMISSION-CATALOG-INVENTORY.csv"
$csvLines = @()
$csvLines += "SeedId,CanonicalKey,Resource,Action,ModuleId,ModuleNameEn,ModuleNameAr,ScreenId,ScreenNameEn,ScreenNameAr,PermissionLevel,LabelEn,LabelAr,DescriptionEn,DescriptionAr,Sensitivity,IsSensitive,RequiredPermissionKeys,IsActive,Scope,IsAssignable,IsTenantAdminBaseline,SourceFile,SourceLine"

$lineNum = 27
$entryCount = 0
$activeCount = 0
$viewCount = 0
$actionCount = 0
$modules = @{}
$scopes = @{}
$baselineCount = 0
$keys = @()

foreach ($entry in $entries) {
    # Skip if not a real entry (just whitespace/comma)
    if ($entry.Trim().Length -lt 50) { continue }
    
    $entryCount++
    
    # Comprehensive regex for the whole entry
    $pattern = '"(b000\d{12})"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]*)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]*)"\s*,\s*PermissionLevel\.(\w+)\s*,\s*"([^"]*)"\s*,\s*"([^"]*)"\s*,\s*"([^"]*)"\s*,\s*"([^"]*)"\s*,\s*"([^"]*)"\s*,\s*(true|false)\s*,\s*\[([^\]]*)\]\s*,\s*(true|false)(?:\s*,\s*PermissionScope\.(\w+))?(?:\s*,\s*IsAssignable:\s*(true|false))?(?:\s*,\s*IsTenantAdminBaseline:\s*(true|false))?'
    
    if ($entry -match $pattern) {
        $seedId = $matches[1]
        $key = $matches[2]
        $resource = $matches[3]
        $action = $matches[4]
        $moduleId = $matches[5]
        $moduleNameEn = $matches[6]
        $moduleNameAr = $matches[7]
        $screenId = $matches[8]
        $screenNameEn = $matches[9]
        $screenNameAr = $matches[10]
        $permLevel = $matches[11]
        $labelEn = $matches[12]
        $labelAr = $matches[13]
        $descEn = $matches[14]
        $descAr = $matches[15]
        $sensitivity = $matches[16]
        $isSensitive = $matches[17]
        $reqKeysStr = $matches[18]
        $isActive = $matches[19]
        $scope = if ($matches[20]) { $matches[20] } else { "Tenant" }
        $isAssignable = if ($matches[21]) { $matches[21] } else { "true" }
        $isBaseline = if ($matches[22]) { $matches[22] } else { "false" }
    } else {
        Write-Host "Failed to parse entry $entryCount"
        continue
    }
    
    # Parse required keys
    $reqKeys = @()
    if ($reqKeysStr.Trim()) {
        $reqKeys = $reqKeysStr.Split(',') | ForEach-Object { $_.Trim().Trim('"') } | Where-Object { $_ }
    }
    
    # Stats
    if ($isActive -eq 'true') { $activeCount++ }
    if ($permLevel -eq 'View') { $viewCount++ } else { $actionCount++ }
    if (-not $modules.ContainsKey($moduleId)) { $modules[$moduleId] = 0 }
    $modules[$moduleId]++
    if (-not $scopes.ContainsKey($scope)) { $scopes[$scope] = 0 }
    $scopes[$scope]++
    if ($isBaseline -eq 'true') { $baselineCount++ }
    $keys += $key
    
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

Write-Host "`nSummary:"
Write-Host "Total entries: $entryCount"
Write-Host "Active: $activeCount"
Write-Host "Inactive: $($entryCount - $activeCount)"
Write-Host "View: $viewCount"
Write-Host "Action: $actionCount"

Write-Host "`nModule breakdown:"
$modules.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Host "  $($_.Name): $($_.Value)" }

Write-Host "`nScope breakdown:"
$scopes.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Host "  $($_.Name): $($_.Value)" }

Write-Host "`nIsTenantAdminBaseline: $baselineCount"

$duplicates = $keys | Group-Object | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Name }
Write-Host "`nDuplicate canonical keys: $($duplicates.Count)"
foreach ($d in $duplicates) { Write-Host "  $d" }