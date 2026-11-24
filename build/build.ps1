# Bouwt Kapture.exe en de tests met de C#-compiler die in Windows zit (.NET Framework 4.8).
# Gebruik: powershell -ExecutionPolicy Bypass -File build\build.ps1
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe" }
$out = Join-Path $root "out"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$refs = "/r:System.dll", "/r:System.Core.dll", "/r:System.Drawing.dll", "/r:System.Windows.Forms.dll"

Write-Host "Logo-icoon maken..."
& $csc /nologo /target:exe "/out:$out\MaakIcoon.exe" @refs "$root\tools\MaakIcoon.cs" "$root\src\Logo.cs" "$root\src\Svg.cs"
if ($LASTEXITCODE -ne 0) { throw "Bouwen van MaakIcoon mislukt" }
& "$out\MaakIcoon.exe" "$out\Kapture.ico" "$out\logo"
if ($LASTEXITCODE -ne 0) { throw "Logo-icoon maken mislukt" }

$src = @(Get-ChildItem (Join-Path $root "src") -Filter *.cs -Recurse | ForEach-Object { $_.FullName })
$res = @("/resource:$out\Kapture.ico,Kapture.ico")

Write-Host "Kapture.exe bouwen..."
& $csc /nologo /target:winexe /optimize+ /unsafe /platform:anycpu /warn:4 `
    "/out:$out\Kapture.exe" "/win32icon:$out\Kapture.ico" "/win32manifest:$root\assets\Kapture.manifest" `
    @refs @res @src
if ($LASTEXITCODE -ne 0) { throw "Bouwen van Kapture.exe mislukt" }

Write-Host "Tests bouwen..."
$tests = @(Get-ChildItem (Join-Path $root "tests") -Filter *.cs | ForEach-Object { $_.FullName })
& $csc /nologo /target:exe /unsafe /platform:anycpu /main:Kapture.Tests "/out:$out\Kapture.Tests.exe" @refs @res @src @tests
if ($LASTEXITCODE -ne 0) { throw "Bouwen van de tests mislukt" }

Write-Host "Tests draaien..."
& "$out\Kapture.Tests.exe"
if ($LASTEXITCODE -ne 0) { throw "Er zijn tests mislukt" }
Write-Host "Klaar: $out\Kapture.exe"
