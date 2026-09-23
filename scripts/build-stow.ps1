param(
    [switch]$Sign,
    [string]$CertificateThumbprint = $env:STOW_SIGNING_CERT_THUMBPRINT
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$version = (Get-Content (Join-Path $root 'STOW_VERSION') -Raw).Trim()
$legalRevision = (Get-Content (Join-Path $root 'LEGAL_TERMS_REVISION') -Raw).Trim()
$project = Join-Path $root 'src\STOW.App\STOW.App.csproj'
$icon = Join-Path $root 'assets\STOW.ico'
$legalStoreSrc = Join-Path $root 'src\STOW.Infrastructure\Configuration\LegalAcceptanceStore.cs'
$setupSrc = Join-Path $root 'src\STOWSetup.cs'
$dist = Join-Path $root 'dist-stow'
$publish = Join-Path $root 'artifacts\stow-publish'
$generatedLegal = Join-Path $root 'src\STOW.App\GeneratedLegal'

if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw 'STOW_VERSION must use semantic version format.'
}
if (-not (Test-Path $icon)) {
    throw "STOW application icon is missing: $icon"
}

$projectText = Get-Content $project -Raw
if ($projectText -notmatch ('<Version>' + [regex]::Escape($version) + '</Version>')) {
    throw "STOW.App version does not match STOW_VERSION=$version"
}

$numericVersion = ($version -split '-')[0]
$fileVersion = "$numericVersion.0"
if ($projectText -notmatch ('<FileVersion>' + [regex]::Escape($fileVersion) + '</FileVersion>')) {
    throw "STOW.App FileVersion does not match $fileVersion"
}

$setupText = Get-Content $setupSrc -Raw
if ($setupText -notmatch ('public const string Version\s*=\s*"' + [regex]::Escape($version) + '"')) {
    throw "STOWSetup.cs Version does not match STOW_VERSION=$version"
}
if ($setupText -notmatch ('public const string FileVersion\s*=\s*"' + [regex]::Escape($fileVersion) + '"')) {
    throw "STOWSetup.cs FileVersion does not match $fileVersion"
}
if ($setupText -notmatch ('AssemblyInformationalVersion\("' + [regex]::Escape($version) + '"\)')) {
    throw "STOWSetup.cs informational version does not match STOW_VERSION=$version"
}

if ([string]::IsNullOrWhiteSpace($legalRevision)) {
    throw 'LEGAL_TERMS_REVISION must not be empty.'
}
if ($setupText -notmatch ('public const string LegalTermsRevision\s*=\s*"' + [regex]::Escape($legalRevision) + '"')) {
    throw "STOWSetup.cs LegalTermsRevision does not match LEGAL_TERMS_REVISION=$legalRevision"
}
$legalStoreText = Get-Content $legalStoreSrc -Raw
if ($legalStoreText -notmatch ('public const string CurrentRevision\s*=\s*"' + [regex]::Escape($legalRevision) + '"')) {
    throw "LegalAcceptanceStore.CurrentRevision does not match LEGAL_TERMS_REVISION=$legalRevision"
}

& dotnet restore $project -r win-x64
if ($LASTEXITCODE -ne 0) { throw "STOW restore failed with exit code $LASTEXITCODE" }

$assetsPath = Join-Path $root 'src\STOW.App\obj\project.assets.json'
if (-not (Test-Path $assetsPath)) { throw 'project.assets.json was not produced.' }
$assets = Get-Content $assetsPath -Raw | ConvertFrom-Json
$framework = $assets.project.frameworks.PSObject.Properties | Select-Object -First 1 -ExpandProperty Value
if (-not $framework) { throw 'Could not resolve STOW target framework assets.' }

function Get-ExactDownloadVersion([string]$packageName) {
    $dependency = $framework.downloadDependencies | Where-Object { $_.name -eq $packageName } | Select-Object -First 1
    if (-not $dependency) { throw "Could not resolve runtime pack $packageName." }
    $raw = [string]$dependency.version
    $clean = $raw.Trim('[',']','(',')')
    return ($clean -split ',')[0].Trim()
}

$coreVersion = Get-ExactDownloadVersion 'Microsoft.NETCore.App.Runtime.win-x64'
$desktopVersion = Get-ExactDownloadVersion 'Microsoft.WindowsDesktop.App.Runtime.win-x64'
$packageRoot = $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($packageRoot)) { throw 'Could not resolve NuGet package root.' }

$coreRoot = Join-Path $packageRoot ("microsoft.netcore.app.runtime.win-x64\" + $coreVersion)
$desktopRoot = Join-Path $packageRoot ("microsoft.windowsdesktop.app.runtime.win-x64\" + $desktopVersion)
if (-not (Test-Path $coreRoot)) { throw "Resolved .NET runtime pack is missing: $coreRoot" }
if (-not (Test-Path $desktopRoot)) { throw "Resolved Windows Desktop runtime pack is missing: $desktopRoot" }

$d3dCompilerPath = Join-Path $desktopRoot 'runtimes\win-x64\native\D3DCompiler_47_cor3.dll'
if (-not (Test-Path $d3dCompilerPath)) {
    throw 'Windows Desktop runtime no longer contains D3DCompiler_47_cor3.dll. Review Windows licensing before publishing.'
}
$d3dCompilerHash = (Get-FileHash $d3dCompilerPath -Algorithm SHA256).Hash.ToLowerInvariant()

if (Test-Path $generatedLegal) { Remove-Item $generatedLegal -Recurse -Force }
New-Item -ItemType Directory -Path $generatedLegal -Force | Out-Null

Copy-Item (Join-Path $coreRoot 'LICENSE.TXT') (Join-Path $generatedLegal 'DOTNET_RUNTIME_LICENSE.txt') -Force
Copy-Item (Join-Path $coreRoot 'THIRD-PARTY-NOTICES.TXT') (Join-Path $generatedLegal 'DOTNET_RUNTIME_THIRD_PARTY_NOTICES.txt') -Force
Copy-Item (Join-Path $desktopRoot 'LICENSE') (Join-Path $generatedLegal 'DOTNET_WINDOWS_DESKTOP_LICENSE.txt') -Force
Copy-Item (Join-Path $root 'licenses\MICROSOFT_DOTNET_LIBRARY_LICENSE.html') (Join-Path $generatedLegal 'MICROSOFT_DOTNET_LIBRARY_LICENSE.html') -Force
Copy-Item (Join-Path $root 'licenses\MICROSOFT_WINDOWS_SDK_LICENSE.html') (Join-Path $generatedLegal 'MICROSOFT_WINDOWS_SDK_LICENSE.html') -Force
Copy-Item (Join-Path $root 'licenses\README.md') (Join-Path $generatedLegal 'MICROSOFT_LICENSE_REFERENCES.md') -Force

$legalManifest = @(
    "STOW_VERSION=$version",
    "LEGAL_TERMS_REVISION=$legalRevision",
    "TARGET=win-x64",
    "DOTNET_RUNTIME_PACK=Microsoft.NETCore.App.Runtime.win-x64/$coreVersion",
    "DOTNET_WINDOWS_DESKTOP_PACK=Microsoft.WindowsDesktop.App.Runtime.win-x64/$desktopVersion",
    "WINDOWS_SDK_COMPONENT=D3DCompiler_47_cor3.dll",
    "WINDOWS_SDK_COMPONENT_SOURCE=Microsoft.WindowsDesktop.App.Runtime.win-x64/$desktopVersion",
    "WINDOWS_SDK_COMPONENT_SHA256=$d3dCompilerHash",
    ""
)
foreach ($file in Get-ChildItem $generatedLegal -File | Sort-Object Name) {
    $hash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $legalManifest += "$hash  $($file.Name)"
}
$legalManifest | Set-Content (Join-Path $generatedLegal 'LEGAL_MANIFEST.txt') -Encoding ascii

foreach ($path in @($dist, $publish)) {
    if (Test-Path $path) { Remove-Item $path -Recurse -Force }
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

& dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publish
if ($LASTEXITCODE -ne 0) { throw "STOW publish failed with exit code $LASTEXITCODE" }

$app = Join-Path $publish 'STOW.exe'
if (-not (Test-Path $app)) { throw 'STOW.exe was not produced.' }

$builtVersion = (Get-Item $app).VersionInfo.FileVersion
if ($builtVersion -ne $fileVersion) {
    throw "Built FileVersion $builtVersion does not match expected $fileVersion"
}

Copy-Item $app (Join-Path $dist 'STOW.exe') -Force

$packageFiles = @(
    'README.md',
    'LICENSE',
    'PRIVACY.md',
    'SECURITY.md',
    'SUPPORT.md',
    'THIRD_PARTY_NOTICES.md',
    'CREDITS.md'
)
foreach ($name in $packageFiles) {
    Copy-Item (Join-Path $root $name) (Join-Path $dist $name) -Force
}
Get-ChildItem $generatedLegal -File | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $dist $_.Name) -Force
}

$legalZip = Join-Path $dist 'STOW-Legal.zip'
$legalNames = @(
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
$legalInputs = $legalNames | ForEach-Object { Join-Path $dist $_ }
foreach ($path in $legalInputs) {
    if (-not (Test-Path $path)) { throw "Missing legal distribution file: $path" }
}
Compress-Archive -Path $legalInputs -DestinationPath $legalZip -CompressionLevel Optimal

$cscCandidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw 'Could not find the .NET Framework C# compiler for STOWSetup.' }

$setup = Join-Path $dist 'STOWSetup.exe'
$payloadResourceArg = '/resource:' + (Join-Path $dist 'STOW.exe') + ',STOW.Payload.exe'
$legalResourceArg = '/resource:' + $legalZip + ',STOW.Legal.zip'
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /win32icon:$icon /out:$setup /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll $payloadResourceArg $legalResourceArg $setupSrc
if ($LASTEXITCODE -ne 0) { throw "STOWSetup compilation failed with exit code $LASTEXITCODE" }

$setupInfo = (Get-Item $setup).VersionInfo
$setupVersion = $setupInfo.FileVersion
if ($setupVersion -ne $fileVersion) {
    throw "STOWSetup FileVersion $setupVersion does not match expected $fileVersion"
}
if ($setupInfo.ProductVersion -ne $version) {
    throw "STOWSetup ProductVersion $($setupInfo.ProductVersion) does not match expected $version"
}

if ($Sign) {
    if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
        throw 'Signed builds require -CertificateThumbprint or STOW_SIGNING_CERT_THUMBPRINT.'
    }
    & (Join-Path $root 'scripts\sign-stow.ps1') `
        -Dist $dist `
        -CertificateThumbprint $CertificateThumbprint
    if ($LASTEXITCODE -ne 0) { throw "STOW signing failed with exit code $LASTEXITCODE" }
}

& (Join-Path $root 'scripts\generate-release-sbom.ps1') -Dist $dist
if ($LASTEXITCODE -ne 0) { throw "SBOM generation failed with exit code $LASTEXITCODE" }

$appHash = (Get-FileHash -Algorithm SHA256 (Join-Path $dist 'STOW.exe')).Hash.ToLowerInvariant()
$setupHash = (Get-FileHash -Algorithm SHA256 (Join-Path $dist 'STOWSetup.exe')).Hash.ToLowerInvariant()
$legalHash = (Get-FileHash -Algorithm SHA256 (Join-Path $dist 'STOW-Legal.zip')).Hash.ToLowerInvariant()
$sbomHash = (Get-FileHash -Algorithm SHA256 (Join-Path $dist 'SBOM.spdx.json')).Hash.ToLowerInvariant()
@(
    "$appHash  STOW.exe",
    "$setupHash  STOWSetup.exe",
    "$legalHash  STOW-Legal.zip",
    "$sbomHash  SBOM.spdx.json"
) | Set-Content (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii

$isPrerelease = $version.Contains('-')
$releaseSummary = if ($isPrerelease -and -not $Sign) {
    'Preview release. This build is intentionally unsigned; Windows SmartScreen may warn. Verify SHA-256 checksums before installation.'
} elseif ($Sign) {
    'Signed release build. Verify Authenticode publisher information and SHA-256 checksums before installation.'
} else {
    'Unsigned local release candidate. Stable publication requires Authenticode signing.'
}
$releaseNotes = @(
    "# STOW $version",
    "",
    $releaseSummary,
    ""
)
$versionNotesPath = Join-Path $root "docs\release\$version.md"
if (Test-Path $versionNotesPath) {
    $releaseNotes += Get-Content $versionNotesPath
} else {
    $releaseNotes += 'A calmer desktop starts here.'
}
$releaseNotes += @(
    "",
    "Licenses, third-party notices and credits are bundled with STOW and are also available in STOW-Legal.zip."
)
$releaseNotes | Set-Content (Join-Path $dist 'release-notes.md') -Encoding utf8

$zip = Join-Path $dist "STOW-$version-win-x64.zip"
$zipInputs = Get-ChildItem $dist -File | Where-Object { $_.FullName -ne $zip } | Select-Object -ExpandProperty FullName
Compress-Archive -Path $zipInputs -DestinationPath $zip -CompressionLevel Optimal

# The copy of SHA256SUMS.txt inside the release ZIP cannot contain the ZIP's
# own hash without becoming self-referential. The external release checksum
# file is therefore finalized only after the ZIP has been created.
$zipHash = (Get-FileHash -Algorithm SHA256 $zip).Hash.ToLowerInvariant()
Add-Content (Join-Path $dist 'SHA256SUMS.txt') "$zipHash  $(Split-Path $zip -Leaf)" -Encoding ascii

Get-Item (Join-Path $dist 'STOW.exe'),(Join-Path $dist 'STOWSetup.exe'),$zip,(Join-Path $dist 'SHA256SUMS.txt') |
    Select-Object Name,Length,LastWriteTime
