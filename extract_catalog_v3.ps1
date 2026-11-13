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

$entries = $catalogContent.Split([string[]]@('new('), [StringSplitOptions]::RemoveEmptyEntries)
Write-Host "Raw entry count: $($entries.Count)"

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
    if ($entry.Trim().Length -lt 50) { continue }
    
    $entryCount++
    
    $allQuotes = [regex]::Matches($entry, '"([^"]*)"')
    if ($allQuotes.Count -lt 15) {
        Write-Host ("Entry {0}: insufficient quoted strings ({1})" -f $entryCount, $allQuotes.Count)
        continue
    }
    
    $seedId = $allQuotes[0].Groups[1].Value
    $key = $allQuotes[1].Groups[1].Value
    $resource = $allQuotes[2].Groups[1].Value
    $action = $allQuotes[3].Groups[1].Value
    $moduleId = $allQuotes[4].Groups[1].Value
    $moduleNameEn = $allQuotes[5].Groups[1].Value
    $moduleNameAr = $allQuotes[6].Groups[1].Value
    $screenId = $allQuotes[7].Groups[1].Value
    $screenNameEn = $allQuotes[8].Groups[1].Value
    $screenNameAr = $allQuotes[9].Groups[1].Value
    $labelEn = $allQuotes[10].Groups[1].Value
    $labelAr = $allQuotes[11].Groups[1].Value
    $descEn = $allQuotes[12].Groups[1].Value
    $descAr = $allQuotes[13].Groups[1].Value
    $sensitivity = $allQuotes[14].Groups[1].Value
    
    $permLevelMatch = [regex]::Match($entry, 'PermissionLevel\.(\w+)')
    $permLevel = if ($permLevelMatch.Success) { $permLevelMatch.Groups[1].Value } else { "View" }
    
    $isSensitiveMatch = [regex]::Match($entry, '"' + [regex]::Escape($sensitivity) + '"\s*,\s*(true|false)')
    $isSensitive = if ($isSensitiveMatch.Success) { $isSensitiveMatch.Groups[1].Value } else { "false" }
    
    $reqKeysMatch = [regex]::Match($entry, '\[([^\]]*)\]')
    $reqKeysStr = if ($reqKeysMatch.Success) { $reqKeysMatch.Groups[1].Value } else { "" }
    
    $isActiveMatch = [regex]::Match($entry, '\]\s*,\s*(true|false)')
    $isActive = if ($isActiveMatch.Success) { $isActiveMatch.Groups[1].Value } else { "true" }
    
    $scopeMatch = [regex]::Match($entry, 'PermissionScope\.(\w+)')
    $scope = if ($scopeMatch.Success) { $scopeMatch.Groups[1].Value } else { "Tenant" }
    
    $isAssignableMatch = [regex]::Match($entry, 'IsAssignable:\s*(true|false)')
    $isAssignable = if ($isAssignableMatch.Success) { $isAssignableMatch.Groups[1].Value } else { "true" }
    
    $isBaselineMatch = [regex]::Match($entry, 'IsTenantAdminBaseline:\s*(true|false)')
    $isBaseline = if ($isBaselineMatch.Success) { $isBaselineMatch.Groups[1].Value } else { "false" }
    
    $reqKeys = @()
    if ($reqKeysStr.Trim()) {
        $reqKeys = $reqKeysStr.Split(',') | ForEach-Object { $_.Trim().Trim('"') } | Where-Object { $_ }
    }
    
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
Write-Host ("Total entries: {0}" -f $entryCount)
Write-Host ("Active: {0}" -f $activeCount)
Write-Host ("Inactive: {0}" -f ($entryCount - $activeCount))
Write-Host ("View: {0}" -f $viewCount)
Write-Host ("Action: {0}" -f $actionCount)

Write-Host "`nModule breakdown:"
$modules.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Host ("  {0}: {1}" -f $_.Name, $_.Value) }

Write-Host "`nScope breakdown:"
$scopes.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Host ("  {0}: {1}" -f $_.Name, $_.Value) }

Write-Host ("`nIsTenantAdminBaseline: {0}" -f $baselineCount)

$duplicates = $keys | Group-Object | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Name }
Write-Host ("`nDuplicate canonical keys: {0}" -f $duplicates.Count)
foreach ($d in $duplicates) { Write-Host ("  {0}" -f $d) }