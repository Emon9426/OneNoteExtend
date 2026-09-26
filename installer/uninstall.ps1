# OneExtend uninstaller - reverses install.ps1.
# Usage: powershell -ExecutionPolicy Bypass -File uninstall.ps1 [-KeepData]
param(
    [string]$BinDir = "$env:LOCALAPPDATA\OneExtend\bin",
    [switch]$KeepData
)
$ErrorActionPreference = 'Continue'
$ProgId = 'OneExtend.Connect'
$AddinKey = "HKCU:\Software\Microsoft\Office\OneNote\AddIns\$ProgId"

function Info($m) { Write-Host "[OneExtend] $m" -ForegroundColor Magenta }

# 1) Remove the OneNote add-in registration
if (Test-Path $AddinKey) {
    Info "Removing $AddinKey"
    Remove-Item -Path $AddinKey -Recurse -Force
}

# 2) Unregister COM (elevated only; per-user keys may not exist)
$dll = Join-Path $BinDir 'OneExtend.Addin.dll'
$elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if ($elevated -and (Test-Path $dll)) {
    foreach ($regasm in @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe") | Where-Object { Test-Path $_ }) {
        Info "Unregistering via $regasm"
        & $regasm /unregister $dll 2>$null | Out-Null
    }
}

# 3) Remove binaries (keep settings/logs unless -KeepData is absent)
if (Test-Path $BinDir) {
    Info "Removing $BinDir"
    Remove-Item -Recurse -Force $BinDir -ErrorAction SilentlyContinue
}
if (-not $KeepData) {
    $data = "$env:APPDATA\OneExtend"
    if (Test-Path $data) {
        Info "Removing $data (settings + logs)"
        Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
    }
}

Info "Uninstalled. Restart OneNote to fully unload."
