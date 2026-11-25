[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must be X.Y.Z.' }
$exe = Join-Path $PSScriptRoot 'bin\Release\MicCast.exe'
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
if ($info.ProductVersion -ne $version) { throw 'Assembly version does not match VERSION.' }
$destination = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item -LiteralPath $exe -Destination (Join-Path $destination 'MicCast.exe')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $destination 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\QUICKSTART.md') -Destination (Join-Path $destination 'QUICKSTART.md')
$checksum = (Get-FileHash -LiteralPath (Join-Path $destination 'MicCast.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $destination 'SHA256SUMS.txt'), "$checksum  MicCast.exe`n", (New-Object Text.UTF8Encoding($false)))
Write-Output "MicCast $version ready: $destination"
