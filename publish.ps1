<#
.SYNOPSIS
    Publishes BluetoothBear as a single self-contained .exe (no .NET install required).
.DESCRIPTION
    The single-file / self-contained settings live in the .csproj. This wrapper
    runs the publish straight into a top-level dist\ folder (git-ignored) and
    prints where the .exe landed.
#>
[CmdletBinding()]
param(
    # Pass -FrameworkDependent for a small (~1 MB) exe that needs the .NET 9 Desktop Runtime.
    [switch]$FrameworkDependent
)

$ErrorActionPreference = 'Stop'
$proj = Join-Path $PSScriptRoot 'src\BluetoothBear\BluetoothBear.csproj'
$dist = Join-Path $PSScriptRoot 'dist'

# A running instance locks the output files.
Get-Process BluetoothBear -ErrorAction SilentlyContinue | Stop-Process -Force

$args = @('publish', $proj, '-c', 'Release', '-r', 'win-x64', '--nologo', '-o', $dist)
if ($FrameworkDependent) { $args += @('--self-contained', 'false', '-p:PublishSingleFile=true') }

& dotnet @args
if ($LASTEXITCODE -ne 0) { throw "publish failed ($LASTEXITCODE)" }

$exe = Join-Path $dist 'BluetoothBear.exe'
$mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host ""
Write-Host "Published: $exe ($mb MB)" -ForegroundColor Green
