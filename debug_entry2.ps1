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

# Check second entry (index 1)
$secondEntry = $entries[1]
Write-Host "Second entry sample (first 800 chars):"
Write-Host $secondEntry.Substring(0, [Math]::Min(800, $secondEntry.Length))

# Try regex on second entry
$pattern = '"(b000\d{12})"'
$match = [regex]::Match($secondEntry, $pattern)
Write-Host "SeedId match: $($match.Success) - $($match.Groups[1].Value)"

# Let's try a different approach - extract all quoted strings
$allQuotes = [regex]::Matches($secondEntry, '"([^"]*)"')
Write-Host "Quoted strings found: $($allQuotes.Count)"
for ($i = 0; $i -lt [Math]::Min(20, $allQuotes.Count); $i++) {
    Write-Host "  [$i] = $($allQuotes[$i].Groups[1].Value)"
}