<#
.SYNOPSIS
    Publishes BluetoothBear as a single self-contained .exe (no .NET install required).
.DESCRIPTION
    The single-file / self-contained settings live in the .csproj, so a plain
    `dotnet publish -c Release` is enough. This wrapper just runs it and prints
    where the .exe landed. Output goes under bin\Release\...\win-x64\publish\
    (git-ignored).
#>
[CmdletBinding()]
param(
    # Pass -FrameworkDependent for a small (~1 MB) exe that needs the .NET 9 Desktop Runtime.
    [switch]$FrameworkDependent
)

$ErrorActionPreference = 'Stop'
$proj = Join-Path $PSScriptRoot 'src\BluetoothBear\BluetoothBear.csproj'

# A running instance locks the output files.
Get-Process BluetoothBear -ErrorAction SilentlyContinue | Stop-Process -Force

$args = @('publish', $proj, '-c', 'Release', '-r', 'win-x64', '--nologo')
if ($FrameworkDependent) { $args += @('--self-contained', 'false', '-p:PublishSingleFile=true') }

& dotnet @args
if ($LASTEXITCODE -ne 0) { throw "publish failed ($LASTEXITCODE)" }

$exe = Join-Path $PSScriptRoot 'src\BluetoothBear\bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\BluetoothBear.exe'
$mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host ""
Write-Host "Published: $exe ($mb MB)" -ForegroundColor Green
