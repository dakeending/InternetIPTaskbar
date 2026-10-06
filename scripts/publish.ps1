<#
.SYNOPSIS
  Publishes a self-contained single-file build without building an installer.
#>
$ErrorActionPreference = 'Stop'
$repo    = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'src\InternetIPTaskbar\InternetIPTaskbar.csproj'
dotnet publish $project -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
Write-Host "`nPublish complete:" -ForegroundColor Green
Write-Host (Join-Path $repo 'src\InternetIPTaskbar\bin\Release\net8.0-windows\win-x64\publish')
