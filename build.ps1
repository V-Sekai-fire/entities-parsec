# Builds WallpaperParsec.exe without a .NET SDK install.
#
# Roslyn is preferred when Visual Studio put one on the machine, but the fallback is the
# C# compiler that ships inside Windows itself, so a clean Windows 11 box can build this.
# The sources stay C# 5-compatible for exactly that reason.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $out | Out-Null

$csc = $null
$roslyn = @(
    "C:\Program Files\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\Roslyn\csc.exe",
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\Roslyn\csc.exe"
)
foreach ($pattern in $roslyn) {
    $hit = Get-ChildItem $pattern -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($hit) { $csc = $hit.FullName; break }
}
if (-not $csc) {
    $inbox = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if (Test-Path $inbox) { $csc = $inbox }
}
if (-not $csc) { throw "No C# compiler found." }

Write-Host "Compiler: $csc"

$sources = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$exe = Join-Path $out 'WallpaperParsec.exe'

$args = @(
    '/nologo'
    '/target:winexe'
    '/platform:anycpu'
    '/optimize+'
    "/out:$exe"
    "/win32manifest:$(Join-Path $root 'app.manifest')"
    '/reference:System.dll'
    '/reference:System.Drawing.dll'
    '/reference:System.Windows.Forms.dll'
) + $sources

& $csc @args
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

Write-Host "Built $exe"
