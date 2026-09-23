param(
    [string]$Dist = (Join-Path (Split-Path -Parent $PSScriptRoot) 'dist-stow')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$version = (Get-Content (Join-Path $root 'STOW_VERSION') -Raw).Trim()
$legalRevision = (Get-Content (Join-Path $root 'LEGAL_TERMS_REVISION') -Raw).Trim()

function Require-File([string]$path) {
    if (-not (Test-Path $path -PathType Leaf)) {
        throw "Required legal/release file is missing: $path"
    }
    if ((Get-Item $path).Length -le 0) {
        throw "Required legal/release file is empty: $path"
    }
}

$rootRequired = @(
    'LEGAL_TERMS_REVISION',
    'LICENSE',
    'PRIVACY.md',
    'SECURITY.md',
    'SUPPORT.md',
    'THIRD_PARTY_NOTICES.md',
    'CREDITS.md',
    'assets\README.md',
    'assets\STOW.png',
    'assets\STOW.ico',
    'scripts\generate-stow-icon.py',
    'licenses\README.md',
    'licenses\MICROSOFT_DOTNET_LIBRARY_LICENSE.html',
    'licenses\MICROSOFT_WINDOWS_SDK_LICENSE.html'
)
foreach ($relative in $rootRequired) {
    Require-File (Join-Path $root $relative)
}

$dotnetLibrarySnapshot = Get-Content (Join-Path $root 'licenses\MICROSOFT_DOTNET_LIBRARY_LICENSE.html') -Raw
if ($dotnetLibrarySnapshot -notmatch 'MICROSOFT\s+\.NET\s+LIBRARY' -or
    $dotnetLibrarySnapshot -notmatch 'DISTRIBUTABLE\s+CODE') {
    throw 'Bundled .NET Library License snapshot does not contain the expected Microsoft license terms.'
}

$windowsSdkSnapshot = Get-Content (Join-Path $root 'licenses\MICROSOFT_WINDOWS_SDK_LICENSE.html') -Raw
if ($windowsSdkSnapshot -notmatch 'MICROSOFT\s+WINDOWS\s+SOFTWARE\s+DEVELOPMENT\s+KIT' -or
    $windowsSdkSnapshot -notmatch 'REDIST\.TXT') {
    throw 'Bundled Windows SDK License snapshot does not contain the expected Microsoft license terms.'
}

$creditsText = Get-Content (Join-Path $root 'CREDITS.md') -Raw
$assetProvenance = Get-Content (Join-Path $root 'assets\README.md') -Raw
if ($creditsText -notmatch 'original project artwork created specifically for STOW' -or
    $assetProvenance -notmatch 'Third-party visual content:\s*none') {
    throw 'STOW brand artwork provenance is not documented in CREDITS.md and assets/README.md.'
}

$appProjectText = Get-Content (Join-Path $root 'src\STOW.App\STOW.App.csproj') -Raw
if ($appProjectText -notmatch '<ApplicationIcon>\.\.\\\.\.\\assets\\STOW\.ico</ApplicationIcon>') {
    throw 'STOW.App must embed assets/STOW.ico as its application icon.'
}
if ($appProjectText -notmatch '<Resource Include="\.\.\\\.\.\\assets\\STOW\.png" Link="Assets\\STOW\.png"\s*/>') {
    throw 'STOW.App must embed the reviewed assets/STOW.png brand artwork.'
}

$releaseBuildText = Get-Content (Join-Path $root 'scripts\build-stow.ps1') -Raw
if ($releaseBuildText -notmatch '/win32icon:\$icon') {
    throw 'STOWSetup must embed the reviewed STOW icon.'
}

# Shipping source projects currently have no external NuGet runtime dependency.
# Any future PackageReference must trigger an explicit licensing review before release.
$runtimeProjects = Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.csproj -File
$runtimePackageRefs = foreach ($project in $runtimeProjects) {
    [xml]$xml = Get-Content $project.FullName -Raw
    foreach ($node in $xml.Project.ItemGroup.PackageReference) {
        if ($null -ne $node) {
            [pscustomobject]@{
                Project = $project.FullName
                Package = [string]$node.Include
            }
        }
    }
}
if ($runtimePackageRefs) {
    $details = ($runtimePackageRefs | ForEach-Object { "$($_.Package) in $($_.Project)" }) -join '; '
    throw "Unreviewed runtime PackageReference detected: $details"
}

# New shipped visual/font assets require provenance and attribution review.
$reviewExtensions = @('.ttf','.otf','.woff','.woff2','.png','.jpg','.jpeg','.svg','.ico','.webp','.gif')
$unreviewedAssets = Get-ChildItem (Join-Path $root 'src\STOW.App') -Recurse -File |
    Where-Object { $reviewExtensions -contains $_.Extension.ToLowerInvariant() }
if ($unreviewedAssets) {
    $details = ($unreviewedAssets.FullName -join '; ')
    throw "Bundled font/image asset requires attribution review before release: $details"
}

$distRequired = @(
    'STOW.exe',
    'STOWSetup.exe',
    'STOW-Legal.zip',
    'SBOM.spdx.json',
    'SHA256SUMS.txt',
    'release-notes.md',
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
foreach ($relative in $distRequired) {
    Require-File (Join-Path $Dist $relative)
}

$manifestPath = Join-Path $Dist 'LEGAL_MANIFEST.txt'
$manifest = Get-Content $manifestPath
if ($manifest -notcontains "STOW_VERSION=$version") {
    throw "LEGAL_MANIFEST.txt does not match STOW_VERSION=$version"
}
if ($manifest -notcontains "LEGAL_TERMS_REVISION=$legalRevision") {
    throw "LEGAL_MANIFEST.txt does not match LEGAL_TERMS_REVISION=$legalRevision"
}
if (-not ($manifest | Where-Object { $_ -match '^DOTNET_RUNTIME_PACK=Microsoft\.NETCore\.App\.Runtime\.win-x64/\d+\.\d+\.\d+$' })) {
    throw 'LEGAL_MANIFEST.txt does not identify the exact .NET runtime pack.'
}
if (-not ($manifest | Where-Object { $_ -match '^DOTNET_WINDOWS_DESKTOP_PACK=Microsoft\.WindowsDesktop\.App\.Runtime\.win-x64/\d+\.\d+\.\d+$' })) {
    throw 'LEGAL_MANIFEST.txt does not identify the exact Windows Desktop runtime pack.'
}
if (-not ($manifest | Where-Object { $_ -eq 'WINDOWS_SDK_COMPONENT=D3DCompiler_47_cor3.dll' })) {
    throw 'LEGAL_MANIFEST.txt does not identify the Windows SDK-licensed D3DCompiler component.'
}
if (-not ($manifest | Where-Object { $_ -match '^WINDOWS_SDK_COMPONENT_SOURCE=Microsoft\.WindowsDesktop\.App\.Runtime\.win-x64/\d+\.\d+\.\d+$' })) {
    throw 'LEGAL_MANIFEST.txt does not identify the D3DCompiler runtime-pack source.'
}
$d3dHashLine = $manifest | Where-Object { $_ -like 'WINDOWS_SDK_COMPONENT_SHA256=*' } | Select-Object -First 1
if (-not $d3dHashLine -or $d3dHashLine.Substring('WINDOWS_SDK_COMPONENT_SHA256='.Length) -notmatch '^[0-9a-fA-F]{64}$') {
    throw 'LEGAL_MANIFEST.txt does not contain a valid D3DCompiler SHA-256.'
}

foreach ($line in $manifest) {
    if ($line -notmatch '^([0-9a-fA-F]{64})\s{2}(.+)$') { continue }
    $expected = $matches[1].ToLowerInvariant()
    $name = $matches[2]
    $path = Join-Path $Dist $name
    Require-File $path
    $actual = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "Legal manifest SHA-256 mismatch for $name"
    }
}

$sbomPath = Join-Path $Dist 'SBOM.spdx.json'
$sbom = Get-Content $sbomPath -Raw | ConvertFrom-Json
if ($sbom.spdxVersion -ne 'SPDX-2.3') { throw 'SBOM is not SPDX 2.3.' }

$stowPackage = $sbom.packages | Where-Object { $_.SPDXID -eq 'SPDXRef-Package-STOW' } | Select-Object -First 1
$corePackage = $sbom.packages | Where-Object { $_.SPDXID -eq 'SPDXRef-Package-DotNetRuntime' } | Select-Object -First 1
$desktopPackage = $sbom.packages | Where-Object { $_.SPDXID -eq 'SPDXRef-Package-WindowsDesktopRuntime' } | Select-Object -First 1
$d3dPackage = $sbom.packages | Where-Object { $_.SPDXID -eq 'SPDXRef-Package-D3DCompiler' } | Select-Object -First 1

if (-not $stowPackage -or $stowPackage.versionInfo -ne $version) {
    throw 'SBOM STOW package version does not match STOW_VERSION.'
}

$coreIdentity = ($manifest | Where-Object { $_ -like 'DOTNET_RUNTIME_PACK=*' } | Select-Object -First 1).Substring('DOTNET_RUNTIME_PACK='.Length)
$desktopIdentity = ($manifest | Where-Object { $_ -like 'DOTNET_WINDOWS_DESKTOP_PACK=*' } | Select-Object -First 1).Substring('DOTNET_WINDOWS_DESKTOP_PACK='.Length)
$coreExpectedVersion = ($coreIdentity -split '/')[1]
$desktopExpectedVersion = ($desktopIdentity -split '/')[1]

if (-not $corePackage -or $corePackage.versionInfo -ne $coreExpectedVersion) {
    throw 'SBOM .NET runtime version does not match LEGAL_MANIFEST.txt.'
}
if (-not $desktopPackage -or $desktopPackage.versionInfo -ne $desktopExpectedVersion) {
    throw 'SBOM Windows Desktop runtime version does not match LEGAL_MANIFEST.txt.'
}
if (-not $d3dPackage) {
    throw 'SBOM does not identify D3DCompiler_47_cor3.dll.'
}
$d3dSbomHash = $d3dPackage.checksums |
    Where-Object { $_.algorithm -eq 'SHA256' } |
    Select-Object -First 1 -ExpandProperty checksumValue
$d3dManifestHash = $d3dHashLine.Substring('WINDOWS_SDK_COMPONENT_SHA256='.Length).ToLowerInvariant()
if ([string]::IsNullOrWhiteSpace($d3dSbomHash) -or $d3dSbomHash.ToLowerInvariant() -ne $d3dManifestHash) {
    throw 'SBOM D3DCompiler checksum does not match LEGAL_MANIFEST.txt.'
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$legalZip = Join-Path $Dist 'STOW-Legal.zip'
$legalArchive = [IO.Compression.ZipFile]::OpenRead($legalZip)
try {
    $legalNames = $legalArchive.Entries | Select-Object -ExpandProperty Name
    foreach ($name in @(
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
    )) {
        if ($legalNames -notcontains $name) {
            throw "STOW-Legal.zip is missing $name"
        }
    }
}
finally {
    $legalArchive.Dispose()
}

$releaseZip = Join-Path $Dist "STOW-$version-win-x64.zip"
Require-File $releaseZip

$sumLines = Get-Content (Join-Path $Dist 'SHA256SUMS.txt')
$zipName = Split-Path $releaseZip -Leaf
$zipLine = $sumLines | Where-Object { $_ -match "^[0-9a-fA-F]{64}\s{2}$([regex]::Escape($zipName))$" } | Select-Object -First 1
if (-not $zipLine) {
    throw "SHA256SUMS.txt does not contain the release ZIP: $zipName"
}
$expectedZipHash = ($zipLine -split '\s+')[0].ToLowerInvariant()
$actualZipHash = (Get-FileHash $releaseZip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualZipHash -ne $expectedZipHash) {
    throw 'Release ZIP SHA-256 does not match SHA256SUMS.txt.'
}

$releaseArchive = [IO.Compression.ZipFile]::OpenRead($releaseZip)
try {
    $releaseNames = $releaseArchive.Entries | Select-Object -ExpandProperty Name
    foreach ($name in $distRequired) {
        if ($name -eq 'release-notes.md' -or $name -eq 'SHA256SUMS.txt' -or $name -eq 'STOW-Legal.zip' -or
            $name -eq 'SBOM.spdx.json' -or $name -match 'LICENSE|NOTICE|CREDITS|PRIVACY|MICROSOFT|LEGAL') {
            if ($releaseNames -notcontains $name) {
                throw "Release ZIP is missing $name"
            }
        }
    }
}
finally {
    $releaseArchive.Dispose()
}

Write-Output "Release legal verification passed for STOW $version."
