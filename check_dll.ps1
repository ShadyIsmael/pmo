$bytes = [IO.File]::ReadAllBytes('apps/api/src/NEXUS.Modules.Documents/bin/Debug/net10.0/NEXUS.Modules.Documents.dll')
$text = [Text.Encoding]::UTF8.GetString($bytes)
Write-Host "Contains TemplateIdentityNotVerified: $($text.Contains('TemplateIdentityNotVerified'))"
$arabic1 = "مرجع كامل"
$idx = $text.IndexOf($arabic1)
if ($idx -ge 0) {
    Write-Host "Found 'مرجع كامل' at index: $idx"
    Write-Host "Context: $($text.Substring($idx, [Math]::Min(100, $text.Length - $idx)))"
} else {
    Write-Host "'مرجع كامل' NOT FOUND"
}
$arabic2 = "القالب المصدري"
$idx2 = $text.IndexOf($arabic2)
if ($idx2 -ge 0) {
    Write-Host "Found 'القالب المصدري' at index: $idx2"
    Write-Host "Context: $($text.Substring($idx2, [Math]::Min(100, $text.Length - $idx2)))"
} else {
    Write-Host "'القالب المصدري' NOT FOUND"
}