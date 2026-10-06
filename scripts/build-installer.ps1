<#
.SYNOPSIS
  Publishes a self-contained single-file build and compiles the Inno Setup installer.
.OUTPUTS
  artifacts\InternetIPTaskbar_Setup_v<version>.exe
#>
$ErrorActionPreference = 'Stop'
$repo       = Split-Path $PSScriptRoot -Parent
$project    = Join-Path $repo 'src\InternetIPTaskbar\InternetIPTaskbar.csproj'
$publishDir = Join-Path $repo 'src\InternetIPTaskbar\bin\Release\net8.0-windows\win-x64\publish'

Write-Host '1/2 Publishing self-contained win-x64 single-file build...' -ForegroundColor Cyan
dotnet publish $project -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
if (-not (Test-Path (Join-Path $publishDir 'InternetIPTaskbar.exe'))) {
    throw 'Publish failed: InternetIPTaskbar.exe not found.'
}

Write-Host '2/2 Building the Inno Setup installer...' -ForegroundColor Cyan
$candidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
)
$iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 compiler (ISCC.exe) not found. Install it from https://jrsoftware.org/isinfo.php' }

$iss = Join-Path $repo 'installer\InternetIPTaskbar.iss'
Write-Host "Inno Setup: $iscc"
& $iscc $iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE." }

Write-Host ''
Write-Host 'INSTALLER BUILD COMPLETED' -ForegroundColor Green
Get-ChildItem (Join-Path $repo 'artifacts') -File | Select-Object FullName, Length
