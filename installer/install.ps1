# OneExtend installer - registers the COM add-in for OneNote desktop.
# Usage:  powershell -ExecutionPolicy Bypass -File install.ps1 [-BinDir <path>]
# Requires: OneNote desktop installed; run from an elevated prompt when
# RegAsm needs machine-wide COM registration (HKCU-only mode below).
param(
    [string]$BinDir = "$env:LOCALAPPDATA\OneExtend\bin",
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$ProgId = 'OneExtend.Connect'
$AddinKey = "HKCU:\Software\Microsoft\Office\OneNote\AddIns\$ProgId"

function Info($m) { Write-Host "[OneExtend] $m" -ForegroundColor Magenta }

# 1) Publish the add-in (carries grammars/ and themes/ as content)
Info "Publishing OneExtend.Addin -> $BinDir"
dotnet publish (Join-Path $RepoRoot 'src\OneExtend.Addin\OneExtend.Addin.csproj') `
    -c Release -o $BinDir | Out-Null
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$dll = Join-Path $BinDir 'OneExtend.Addin.dll'
if (-not (Test-Path $dll)) { throw "Published DLL not found: $dll" }

# 2) COM registration via RegAsm (try both bitnesses that exist)
$regasms = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe"
) | Where-Object { Test-Path $_ }

if ($regasms.Count -eq 0) { throw "RegAsm not found - is .NET Framework 4.x installed?" }

$elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

foreach ($regasm in $regasms) {
    if (-not $elevated) {
        Info "Skipping $regasm (needs elevation). Re-run from an elevated prompt for COM registration."
        break
    }
    Info "Registering via $regasm"
    & $regasm /codebase $dll | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "RegAsm failed for $dll" }
}

# 3) OneNote add-in registration (per-user, no elevation needed)
Info "Writing $AddinKey"
New-Item -Path $AddinKey -Force | Out-Null
Set-ItemProperty -Path $AddinKey -Name 'FriendlyName' -Value 'OneExtend (code blocks for OneNote)'
Set-ItemProperty -Path $AddinKey -Name 'Description' -Value 'Insert syntax-highlighted code blocks into OneNote. https://github.com/Emon9426/OneNoteExtend'
Set-ItemProperty -Path $AddinKey -Name 'LoadBehavior' -Value 3

Info "Installed. Start OneNote desktop and press Ctrl+Alt+C to insert a code block."
if (-not $elevated) {
    Info "NOTE: COM classes were not registered (no admin). Run once from an elevated prompt:"
    Info "      powershell -ExecutionPolicy Bypass -File install.ps1"
}
