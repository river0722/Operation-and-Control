param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Test
)

$ErrorActionPreference = 'Stop'
$projectRoot = Join-Path $PSScriptRoot 'Operations Control'
$project = Join-Path $projectRoot 'Operations Control.csproj'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = $null
if (Test-Path -LiteralPath $vswhere) {
    $msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
}
if (-not $msbuild) {
    $command = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($command) { $msbuild = $command.Source }
}
if (-not $msbuild) {
    $msbuild = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\MSBuild.exe'
}
if (-not (Test-Path -LiteralPath $msbuild)) {
    throw 'MSBuild was not found. Install Visual Studio with .NET desktop development tools.'
}

& $msbuild $project /t:Build "/p:Configuration=$Configuration" /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
$output = Join-Path $projectRoot "bin\$Configuration"
$application = Join-Path $output 'Operations Control.exe'
Write-Output "Application: $application"

if ($Test) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v3.5\csc.exe'
    if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 3.5 compiler was not found.' }
    $runner = Join-Path $output 'OperationsControl.SmokeTests.exe'
    $source = Join-Path $projectRoot 'Tests\SmokeTests.cs'
    & $compiler /nologo /target:exe /platform:x86 "/out:$runner" "/reference:$application" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Design.dll $source
    if ($LASTEXITCODE -ne 0) { throw 'Smoke test compilation failed.' }
    & $runner (Join-Path $PSScriptRoot 'screenshots')
    if ($LASTEXITCODE -ne 0) { throw 'Smoke tests failed.' }
}
