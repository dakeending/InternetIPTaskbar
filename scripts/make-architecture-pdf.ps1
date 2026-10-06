# Builds docs/ARCHITECTURE.ru.pdf from docs/architecture.ru.html using headless Microsoft Edge or Chrome.
$ErrorActionPreference = 'Stop'
$html = (Resolve-Path (Join-Path $PSScriptRoot '..\docs\architecture.ru.html')).Path
$pdf  = Join-Path (Split-Path $html) 'ARCHITECTURE.ru.pdf'

$browser = @(
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $browser) { throw 'Neither Microsoft Edge nor Google Chrome was found.' }

if (Test-Path $pdf) { Remove-Item $pdf -Force }
$profile = Join-Path $env:TEMP ('arch-pdf-' + [guid]::NewGuid())
$uri = ([System.Uri]$html).AbsoluteUri
& $browser --headless=new --disable-gpu --no-pdf-header-footer "--user-data-dir=$profile" "--print-to-pdf=$pdf" $uri | Out-Null
# the headless browser exits asynchronously: wait for the file to appear
for ($i = 0; $i -lt 30 -and -not (Test-Path $pdf); $i++) { Start-Sleep -Milliseconds 500 }
Remove-Item $profile -Recurse -Force -ErrorAction SilentlyContinue
if (-not (Test-Path $pdf)) { throw 'PDF was not created.' }
Write-Host "PDF: $pdf ($([math]::Round((Get-Item $pdf).Length / 1KB)) KB)" -ForegroundColor Green
