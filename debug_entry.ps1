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

# Test regex on first entry
$firstEntry = $entries[0]
Write-Host "First entry sample (first 500 chars):"
Write-Host $firstEntry.Substring(0, [Math]::Min(500, $firstEntry.Length))

# Try simpler regex - just find all the quoted strings in order
$pattern = '"(b000\d{12})"'
$match = [regex]::Match($firstEntry, $pattern)
Write-Host "SeedId match: $($match.Success) - $($match.Groups[1].Value)"

# Let's try a different approach - extract all quoted strings
$allQuotes = [regex]::Matches($firstEntry, '"([^"]*)"')
Write-Host "Quoted strings found: $($allQuotes.Count)"
for ($i = 0; $i -lt $allQuotes.Count; $i++) {
    Write-Host "  [$i] = $($allQuotes[$i].Groups[1].Value)"
}