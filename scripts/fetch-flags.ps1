<#
.SYNOPSIS
  Downloads country flags (w80 PNG, about 250 files, ~0.5 MB) from flagcdn.com into src\InternetIPTaskbar\Flags.
.DESCRIPTION
  The next build embeds them in the exe, so the program no longer depends on flagcdn.com at run time.
  The flags are not stored in the repository (see .gitignore); run this once before a release build.
  Flag images are provided by https://flagcdn.com - check their terms before redistributing the result.
#>
$ErrorActionPreference = 'Stop'
$dir = Join-Path (Split-Path $PSScriptRoot -Parent) 'src\InternetIPTaskbar\Flags'
New-Item -ItemType Directory -Force $dir | Out-Null

$codes = Invoke-RestMethod 'https://flagcdn.com/en/codes.json'
$ok = 0; $fail = @()
foreach ($code in $codes.PSObject.Properties.Name) {
    if ($code -notmatch '^[a-z]{2}$') { continue }   # skip sub-national regions such as us-ca
    try {
        Invoke-WebRequest "https://flagcdn.com/w80/$code.png" -OutFile (Join-Path $dir "$code.png") -UseBasicParsing
        $ok++
    } catch { $fail += $code }
}
Write-Host "Downloaded: $ok" -ForegroundColor Green
if ($fail) { Write-Host "Failed: $($fail -join ', ')" -ForegroundColor Yellow }
