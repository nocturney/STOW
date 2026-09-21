$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$setup = Join-Path $root 'dist-stow\STOWSetup.exe'
$installDir = Join-Path $env:LOCALAPPDATA 'STOW'
$app = Join-Path $installDir 'STOW.exe'
$uninstaller = Join-Path $installDir 'Uninstall.exe'
$config = Join-Path $env:APPDATA 'STOW\config.txt'
$result = Join-Path $root 'artifacts\installer-regression.log'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$arpKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\STOW'

New-Item -ItemType Directory -Path (Split-Path $result) -Force | Out-Null
$lines = New-Object System.Collections.Generic.List[string]
$originalConfigHash = (Get-FileHash $config -Algorithm SHA256).Hash
$lines.Add("CONFIG_BEFORE=$originalConfigHash")

function Add-Check([string]$name, [bool]$ok, [string]$detail = '') {
    $lines.Add("$name=" + ($(if ($ok) { 'PASS' } else { 'FAIL' })) + $(if ($detail) { " $detail" } else { '' }))
    if (-not $ok) { throw "Regression check failed: $name $detail" }
}

$failed = $false
try {
    $p = Start-Process -FilePath $uninstaller -ArgumentList '/UNINSTALL','/VERYSILENT' -Wait -PassThru
    Add-Check 'UNINSTALL_EXIT' ($p.ExitCode -eq 0) "code=$($p.ExitCode)"

    $deadline = (Get-Date).AddSeconds(8)
    while ((Test-Path $installDir) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 250 }
    Add-Check 'INSTALL_DIR_REMOVED' (-not (Test-Path $installDir))
    Add-Check 'CONFIG_RETAINED' (Test-Path $config)
    $afterUninstallHash = (Get-FileHash $config -Algorithm SHA256).Hash
    Add-Check 'CONFIG_UNCHANGED_AFTER_UNINSTALL' ($afterUninstallHash -eq $originalConfigHash) $afterUninstallHash
    $startupAfterUninstall = (Get-ItemProperty $runKey -ErrorAction SilentlyContinue).STOW
    Add-Check 'STARTUP_REMOVED' ([string]::IsNullOrWhiteSpace($startupAfterUninstall))
    Add-Check 'ARP_REMOVED' (-not (Test-Path $arpKey))

    $p = Start-Process -FilePath $setup -ArgumentList '/VERYSILENT','/NOLAUNCH' -Wait -PassThru
    Add-Check 'REINSTALL_EXIT' ($p.ExitCode -eq 0) "code=$($p.ExitCode)"
    Add-Check 'APP_REINSTALLED' (Test-Path $app)
    Add-Check 'UNINSTALLER_REINSTALLED' (Test-Path $uninstaller)
    Add-Check 'CONFIG_RETAINED_AFTER_REINSTALL' (Test-Path $config)
    $afterReinstallHash = (Get-FileHash $config -Algorithm SHA256).Hash
    Add-Check 'CONFIG_UNCHANGED_AFTER_REINSTALL' ($afterReinstallHash -eq $originalConfigHash) $afterReinstallHash

    $arp = Get-ItemProperty $arpKey -ErrorAction SilentlyContinue
    Add-Check 'ARP_REGISTERED' ($null -ne $arp)
    Add-Check 'ARP_VERSION' ($arp.DisplayVersion -eq '0.4.0-preview.1') $arp.DisplayVersion
    $startup = (Get-ItemProperty $runKey -ErrorAction SilentlyContinue).STOW
    Add-Check 'STARTUP_RESTORED' (-not [string]::IsNullOrWhiteSpace($startup)) $startup

    Start-Process -FilePath $app -ArgumentList '--background'
    Start-Sleep -Milliseconds 800
    Add-Check 'INSTALLED_APP_STARTS' ($null -ne (Get-Process STOW -ErrorAction SilentlyContinue))
}
catch {
    $failed = $true
    $lines.Add("ERROR=$($_.Exception.Message)")
}

finally {
    if (-not (Test-Path $app)) {
        try {
            $recover = Start-Process -FilePath $setup -ArgumentList '/VERYSILENT','/NOLAUNCH' -Wait -PassThru
            $lines.Add("RECOVERY_INSTALL_EXIT=$($recover.ExitCode)")
        } catch {
            $lines.Add("RECOVERY_INSTALL_ERROR=$($_.Exception.Message)")
        }
    }
    if ((Test-Path $app) -and -not (Get-Process STOW -ErrorAction SilentlyContinue)) {
        try { Start-Process -FilePath $app -ArgumentList '--background' } catch { }
    }

    $lines.Add("RESULT=" + $(if ($failed) { 'FAIL' } else { 'PASS' }))
    $lines | Set-Content $result
}

if ($failed) { exit 1 }
exit 0
