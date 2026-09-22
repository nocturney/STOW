$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$setup = Join-Path $root 'dist-stow\STOWSetup.exe'
$expectedVersion = (Get-Content (Join-Path $root 'STOW_VERSION') -Raw).Trim()
$installDir = Join-Path $env:LOCALAPPDATA 'STOW'
$app = Join-Path $installDir 'STOW.exe'
$uninstaller = Join-Path $installDir 'Uninstall.exe'
$config = Join-Path $env:APPDATA 'STOW\config.txt'
$legalAcceptance = Join-Path $env:APPDATA 'STOW\legal-acceptance.txt'
$legalRevision = (Get-Content (Join-Path $root 'LEGAL_TERMS_REVISION') -Raw).Trim()
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

    if (Test-Path $legalAcceptance) { Remove-Item $legalAcceptance -Force }
    Add-Check 'FRESH_INSTALL_HAS_NO_ACCEPTANCE' (-not (Test-Path $legalAcceptance))

    $p = Start-Process -FilePath $setup -ArgumentList '/VERYSILENT','/NOLAUNCH' -Wait -PassThru
    Add-Check 'FIRST_INSTALL_WITHOUT_LICENSES_REJECTED' ($p.ExitCode -ne 0) "code=$($p.ExitCode)"
    Add-Check 'REJECTED_INSTALL_LEFT_NO_APP' (-not (Test-Path $app))

    $p = Start-Process -FilePath $setup -ArgumentList '/VERYSILENT','/NOLAUNCH','/ACCEPTLICENSES=1' -Wait -PassThru
    Add-Check 'REINSTALL_EXIT' ($p.ExitCode -eq 0) "code=$($p.ExitCode)"
    Add-Check 'APP_REINSTALLED' (Test-Path $app)
    Add-Check 'UNINSTALLER_REINSTALLED' (Test-Path $uninstaller)
    Add-Check 'CONFIG_RETAINED_AFTER_REINSTALL' (Test-Path $config)
    $afterReinstallHash = (Get-FileHash $config -Algorithm SHA256).Hash
    Add-Check 'CONFIG_UNCHANGED_AFTER_REINSTALL' ($afterReinstallHash -eq $originalConfigHash) $afterReinstallHash

    $legalDir = Join-Path $installDir 'legal'
    Add-Check 'LEGAL_DIR_REINSTALLED' (Test-Path $legalDir)
    $requiredLegal = @(
        'LICENSE',
        'PRIVACY.md',
        'SECURITY.md',
        'SUPPORT.md',
        'THIRD_PARTY_NOTICES.md',
        'CREDITS.md',
        'DOTNET_RUNTIME_LICENSE.txt',
        'DOTNET_RUNTIME_THIRD_PARTY_NOTICES.txt',
        'DOTNET_WINDOWS_DESKTOP_LICENSE.txt',
        'MICROSOFT_DOTNET_LIBRARY_LICENSE.html',
        'MICROSOFT_WINDOWS_SDK_LICENSE.html',
        'MICROSOFT_LICENSE_REFERENCES.md',
        'LEGAL_MANIFEST.txt'
    )
    foreach ($name in $requiredLegal) {
        Add-Check ("LEGAL_" + ($name -replace '[^A-Za-z0-9]','_').ToUpperInvariant()) (Test-Path (Join-Path $legalDir $name))
    }
    $legalManifest = Get-Content (Join-Path $legalDir 'LEGAL_MANIFEST.txt') -Raw
    Add-Check 'LEGAL_MANIFEST_VERSION' ($legalManifest -match [regex]::Escape("STOW_VERSION=$expectedVersion"))
    Add-Check 'LEGAL_MANIFEST_CORE_RUNTIME' ($legalManifest -match 'DOTNET_RUNTIME_PACK=Microsoft\.NETCore\.App\.Runtime\.win-x64/\d+\.\d+\.\d+')
    Add-Check 'LEGAL_MANIFEST_DESKTOP_RUNTIME' ($legalManifest -match 'DOTNET_WINDOWS_DESKTOP_PACK=Microsoft\.WindowsDesktop\.App\.Runtime\.win-x64/\d+\.\d+\.\d+')
    Add-Check 'LEGAL_MANIFEST_D3D_COMPONENT' ($legalManifest -match '(?m)^WINDOWS_SDK_COMPONENT=D3DCompiler_47_cor3\.dll\r?$')
    Add-Check 'LEGAL_MANIFEST_D3D_SOURCE' ($legalManifest -match '(?m)^WINDOWS_SDK_COMPONENT_SOURCE=Microsoft\.WindowsDesktop\.App\.Runtime\.win-x64/\d+\.\d+\.\d+\r?$')
    Add-Check 'LEGAL_MANIFEST_D3D_HASH' ($legalManifest -match '(?m)^WINDOWS_SDK_COMPONENT_SHA256=[0-9a-fA-F]{64}\r?$')

    Add-Check 'LEGAL_ACCEPTANCE_FILE_CREATED' (Test-Path $legalAcceptance)
    $acceptanceText = Get-Content $legalAcceptance -Raw
    Add-Check 'LEGAL_ACCEPTANCE_REVISION' ($acceptanceText -match [regex]::Escape("REVISION=$legalRevision"))
    Add-Check 'LEGAL_ACCEPTANCE_SOURCE' ($acceptanceText -match '(?m)^SOURCE=installer\r?$')

    $installerKey = 'HKCU:\Software\STOW\Installer'
    $installerState = Get-ItemProperty $installerKey -ErrorAction SilentlyContinue
    Add-Check 'LICENSE_ACCEPTANCE_RECORDED' ($installerState.AcceptedLicensesVersion -eq $expectedVersion) $installerState.AcceptedLicensesVersion

    $arp = Get-ItemProperty $arpKey -ErrorAction SilentlyContinue
    Add-Check 'ARP_REGISTERED' ($null -ne $arp)
    Add-Check 'ARP_VERSION' ($arp.DisplayVersion -eq $expectedVersion) $arp.DisplayVersion
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
            $recover = Start-Process -FilePath $setup -ArgumentList '/VERYSILENT','/NOLAUNCH','/ACCEPTLICENSES=1' -Wait -PassThru
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
