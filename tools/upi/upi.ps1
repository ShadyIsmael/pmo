param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

$ErrorActionPreference = 'Stop'
$candidates = @()

if ($env:UPI_PYTHON -and (Test-Path -LiteralPath $env:UPI_PYTHON -PathType Leaf)) {
    $candidates += [pscustomobject]@{ Executable = $env:UPI_PYTHON; Prefix = @() }
}

foreach ($name in @('python.exe', 'python3.exe', 'py.exe')) {
    $command = Get-Command $name -ErrorAction SilentlyContinue
    if ($command -and $command.Source -notmatch '\\WindowsApps\\') {
        $prefix = if ($name -eq 'py.exe') { @('-3') } else { @() }
        $candidates += [pscustomobject]@{ Executable = $command.Source; Prefix = $prefix }
    }
}

# Codex ships a local Python runtime on this device; use it only when no regular
# Python installation or launcher is available on PATH.
$codexPython = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (Test-Path -LiteralPath $codexPython) {
    $candidates += [pscustomobject]@{ Executable = $codexPython; Prefix = @() }
}

$selected = $null
foreach ($candidate in $candidates) {
    try {
        $probeArgs = @($candidate.Prefix) + @('--version')
        $versionText = & $candidate.Executable @probeArgs 2>$null
        $versionExitCode = $LASTEXITCODE
        if ($versionExitCode -eq 0 -and "$versionText" -match 'Python\s+(\d+)\.(\d+)') {
            $major = [int]$Matches[1]
            $minor = [int]$Matches[2]
            if ($major -gt 3 -or ($major -eq 3 -and $minor -ge 10)) {
                $selected = $candidate
                break
            }
        }
    } catch {
        # Ignore inaccessible application aliases and try the next interpreter.
    }
}

if (-not $selected) {
    Write-Error 'UPI requires Python 3.10 or newer. Install Python or set UPI_PYTHON to its executable path.'
    exit 2
}

$engine = Join-Path $PSScriptRoot 'upi.py'
$invokeArgs = @($selected.Prefix) + @($engine) + @($Arguments)
& $selected.Executable @invokeArgs
exit $LASTEXITCODE
