$ErrorActionPreference = 'Continue'
$OutputEncoding = [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$env:PYTHONIOENCODING = 'utf-8'
$candidates = New-Object 'System.Collections.Generic.List[string]'
if ($env:PMAC_NXLINK_PYTHON) { $candidates.Add($env:PMAC_NXLINK_PYTHON) }
$candidates.Add((Join-Path $PSScriptRoot '.venv\Scripts\python.exe'))
$launcher = Get-Command py.exe -ErrorAction SilentlyContinue
if ($launcher) {
    $detected = & $launcher.Source -3.13 -c 'import sys; print(sys.executable)' 2>$null
    if ($LASTEXITCODE -eq 0 -and $detected) { $candidates.Add([string]$detected) }
}
foreach ($hive in @('HKCU:', 'HKLM:')) {
    foreach ($root in @('Software\Python', 'Software\WOW6432Node\Python')) {
        Get-ItemProperty "$hive\$root\*\*\InstallPath" -ErrorAction SilentlyContinue | ForEach-Object {
            if ($_.ExecutablePath) { $candidates.Add($_.ExecutablePath) }
            if ($_.'(default)') { $candidates.Add((Join-Path $_.'(default)' 'python.exe')) }
        }
    }
}
foreach ($name in @('python.exe', 'python3.13.exe', 'python3.exe')) {
    Get-Command $name -All -ErrorAction SilentlyContinue | ForEach-Object { $candidates.Add($_.Source) }
}
foreach ($candidate in ($candidates | Select-Object -Unique)) {
    if (-not (Test-Path -LiteralPath $candidate)) { continue }
    & $candidate -I -c 'import sys; sys.exit(0 if sys.version_info[:2] == (3,13) else 1)' 2>$null
    if ($LASTEXITCODE -ne 0) { continue }
    Write-Host "Python 3.13: $candidate"
    & $candidate -u (Join-Path $PSScriptRoot 'bootstrap.py') --setup-only
    exit $LASTEXITCODE
}
Write-Error 'No working Python 3.13 found. Install Python 3.13 on this computer and retry.'
exit 1
