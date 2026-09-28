param(
  [int]$DurationSeconds = 150,
  [int]$IntervalSeconds = 5,
  [string]$OutputPath = '.\tests\load\results\server-metrics.csv'
)

$parent = Split-Path -Parent $OutputPath
if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }

function Get-ProcessSnapshot([string]$Name) {
  $processes = @(Get-Process -Name $Name -ErrorAction SilentlyContinue)
  $cpuSeconds = 0.0
  if ($processes.Count -gt 0) {
    $cpuSeconds = ($processes | ForEach-Object { $_.TotalProcessorTime.TotalSeconds } | Measure-Object -Sum).Sum
  }
  [pscustomobject]@{
    Count = $processes.Count
    WorkingSetMB = [math]::Round((($processes | Measure-Object -Property WorkingSet64 -Sum).Sum / 1MB), 1)
    CpuSeconds = [math]::Round($cpuSeconds, 3)
  }
}

function Get-CounterValue([string]$Path) {
  try { return [math]::Round((Get-Counter -Counter $Path -ErrorAction Stop).CounterSamples[0].CookedValue, 3) }
  catch { return $null }
}

function New-Row($previous, $current, [int]$ElapsedSeconds) {
  $processorCount = [Environment]::ProcessorCount
  $dotnetCpu = if ($previous.Dotnet.CpuSeconds -ne $null) { [math]::Round(100 * ($current.Dotnet.CpuSeconds - $previous.Dotnet.CpuSeconds) / ($ElapsedSeconds * $processorCount), 2) } else { $null }
  $sqlCpu = if ($previous.Sql.CpuSeconds -ne $null) { [math]::Round(100 * ($current.Sql.CpuSeconds - $previous.Sql.CpuSeconds) / ($ElapsedSeconds * $processorCount), 2) } else { $null }
  [pscustomobject]@{
    Timestamp = (Get-Date).ToString('o')
    IntervalSeconds = $ElapsedSeconds
    DotnetProcessCount = $current.Dotnet.Count
    DotnetWorkingSetMB = $current.Dotnet.WorkingSetMB
    DotnetCpuPctAllCores = $dotnetCpu
    SqlServerProcessCount = $current.Sql.Count
    SqlServerWorkingSetMB = $current.Sql.WorkingSetMB
    SqlServerCpuPctAllCores = $sqlCpu
    SqlUserConnections = Get-CounterValue '\SQLServer:General Statistics\User Connections'
  }
}

$previous = [pscustomobject]@{
  Dotnet = Get-ProcessSnapshot 'dotnet'
  Sql = Get-ProcessSnapshot 'sqlservr'
}
$rows = @()
$deadline = (Get-Date).AddSeconds($DurationSeconds)
while ((Get-Date) -lt $deadline) {
  Start-Sleep -Seconds $IntervalSeconds
  $current = [pscustomobject]@{
    Dotnet = Get-ProcessSnapshot 'dotnet'
    Sql = Get-ProcessSnapshot 'sqlservr'
  }
  $rows += New-Row $previous $current $IntervalSeconds
  $previous = $current
}
$rows | Export-Csv -NoTypeInformation -Path $OutputPath
