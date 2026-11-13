$content = Get-Content -Path 'C:\Projects\pmo\apps\api\src\NEXUS.Modules.Identity\Application\PermissionCatalog.cs' -Raw
$startMarker = 'private static readonly IReadOnlyList<PermissionCatalogEntry> Catalog ='
$start = $content.IndexOf($startMarker)
Write-Host "Start: $start"
if ($start -gt 0) {
    $catalogStart = $content.IndexOf('[', $start)
    Write-Host "Catalog start: $catalogStart"
    $sample = $content.Substring($catalogStart, 2000)
    Write-Host "Sample: $sample"
}