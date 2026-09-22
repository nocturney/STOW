param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Dist = '',
    [string]$OutputRoot = '',
    [switch]$AllowPrerelease
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Dist)) {
    $Dist = Join-Path $root 'dist-stow'
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $root 'packaging\generated'
}

if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw 'Version must be SemVer without a leading v.' }
if ($Version.Contains('-') -and -not $AllowPrerelease) { throw 'Package-manager metadata defaults to stable releases. Pass -AllowPrerelease explicitly for a prerelease.' }

$sums = Join-Path $Dist 'SHA256SUMS.txt'
if (-not (Test-Path $sums)) { throw "Missing $sums" }
$line = Get-Content $sums | Where-Object { $_ -match '\s+STOW\.exe$' } | Select-Object -First 1
if (-not $line) { throw 'SHA256SUMS.txt has no STOW.exe entry.' }
$hash = (($line -split '\s+')[0]).ToUpperInvariant()
if ($hash -notmatch '^[0-9A-F]{64}$') { throw 'Invalid STOW.exe SHA-256.' }

$releaseBase = "https://github.com/nocturney/STOW/releases/download/v$Version"
$exeUrl = "$releaseBase/STOW.exe"
$winget = Join-Path $OutputRoot "winget\$Version"
$scoop = Join-Path $OutputRoot 'scoop'
New-Item -ItemType Directory -Path $winget,$scoop -Force | Out-Null

@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.1.12.0.schema.json
PackageIdentifier: ChristianVelvet.STOW
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.12.0
"@ | Set-Content (Join-Path $winget 'ChristianVelvet.STOW.yaml') -Encoding utf8

@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.1.12.0.schema.json
PackageIdentifier: ChristianVelvet.STOW
PackageVersion: $Version
InstallerType: portable
Commands:
- STOW
Installers:
- Architecture: x64
  InstallerUrl: $exeUrl
  InstallerSha256: $hash
ManifestType: installer
ManifestVersion: 1.12.0
"@ | Set-Content (Join-Path $winget 'ChristianVelvet.STOW.installer.yaml') -Encoding utf8

@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.1.12.0.schema.json
PackageIdentifier: ChristianVelvet.STOW
PackageVersion: $Version
PackageLocale: en-US
Publisher: Christian Velvet
PublisherUrl: https://github.com/nocturney
PublisherSupportUrl: https://github.com/nocturney/STOW/issues
PrivacyUrl: https://github.com/nocturney/STOW/blob/main/PRIVACY.md
Author: Christian Velvet
PackageName: STOW
PackageUrl: https://github.com/nocturney/STOW
License: MIT
LicenseUrl: https://github.com/nocturney/STOW/blob/main/LICENSE
ShortDescription: Keep running Windows apps out of the way by stowing them to the system tray.
Description: STOW is a lightweight Windows utility that lets selected desktop applications leave the taskbar and remain available from the system tray when minimized.
Moniker: stow
Tags:
- minimize
- system-tray
- tray
- productivity
- utility
- windows
Documentations:
- DocumentLabel: Third-party notices and credits
  DocumentUrl: https://github.com/nocturney/STOW/blob/main/THIRD_PARTY_NOTICES.md
- DocumentLabel: Privacy
  DocumentUrl: https://github.com/nocturney/STOW/blob/main/PRIVACY.md
ReleaseNotesUrl: https://github.com/nocturney/STOW/releases/tag/v$Version
InstallationNotes: STOW shows a one-time first-run acknowledgment for its MIT License and applicable bundled third-party terms. The terms remain available offline from About.
ManifestType: defaultLocale
ManifestVersion: 1.12.0
"@ | Set-Content (Join-Path $winget 'ChristianVelvet.STOW.locale.en-US.yaml') -Encoding utf8

$scoopManifest = [ordered]@{
    version = $Version
    description = 'Keep running Windows apps out of the way by stowing them to the system tray.'
    homepage = 'https://github.com/nocturney/STOW'
    license = 'MIT'
    url = $exeUrl
    hash = $hash.ToLowerInvariant()
    shortcuts = @(, @('STOW.exe','STOW'))
    checkver = @{ github = 'https://github.com/nocturney/STOW' }
    autoupdate = @{ url = 'https://github.com/nocturney/STOW/releases/download/v$version/STOW.exe' }
    notes = "Scoop installs STOW in portable mode. STOW shows a one-time first-run acknowledgment for its MIT License and applicable bundled third-party terms. Use 'scoop update stow' for upgrades. Third-party notices: https://github.com/nocturney/STOW/blob/main/THIRD_PARTY_NOTICES.md"
}
$scoopManifest | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $scoop 'stow.json') -Encoding utf8

Write-Output "Generated WinGet metadata: $winget"
Write-Output "Generated Scoop metadata: $(Join-Path $scoop 'stow.json')"
Write-Output "STOW.exe SHA256: $hash"

$scoopPath = Join-Path $scoop 'stow.json'
$parsedScoop = Get-Content $scoopPath -Raw | ConvertFrom-Json
if ($parsedScoop.version -ne $Version) { throw 'Generated Scoop version mismatch.' }
if ($parsedScoop.hash.ToUpperInvariant() -ne $hash) { throw 'Generated Scoop hash mismatch.' }
if ($parsedScoop.shortcuts.Count -ne 1 -or $parsedScoop.shortcuts[0].Count -ne 2 -or $parsedScoop.shortcuts[0][0] -ne 'STOW.exe' -or $parsedScoop.shortcuts[0][1] -ne 'STOW') {
    throw 'Generated Scoop shortcut structure is invalid.'
}

$installerManifest = Get-Content (Join-Path $winget 'ChristianVelvet.STOW.installer.yaml') -Raw
if ($installerManifest -notmatch [regex]::Escape("InstallerSha256: $hash")) { throw 'Generated WinGet hash mismatch.' }
if ($installerManifest -notmatch [regex]::Escape("InstallerUrl: $exeUrl")) { throw 'Generated WinGet URL mismatch.' }
