$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$version = (Get-Content (Join-Path $root 'STOW_VERSION') -Raw).Trim()
$project = Join-Path $root 'src\STOW.App\STOW.App.csproj'
$dist = Join-Path $root 'dist-stow'
$publish = Join-Path $root 'artifacts\stow-publish'

if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw 'STOW_VERSION must use semantic version format.'
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
    'THIRD_PARTY_NOTICES.md'
)
foreach ($name in $packageFiles) {
    Copy-Item (Join-Path $root $name) (Join-Path $dist $name) -Force
}

$nugetRoot = Join-Path $env:USERPROFILE '.nuget\packages'
$corePackageRoot = Join-Path $nugetRoot 'microsoft.netcore.app.runtime.win-x64'
$desktopPackageRoot = Join-Path $nugetRoot 'microsoft.windowsdesktop.app.runtime.win-x64'
$coreRoot = Get-ChildItem $corePackageRoot -Directory | Sort-Object { [version](($_.Name -split '-')[0]) } -Descending | Select-Object -First 1 -ExpandProperty FullName
$desktopRoot = Get-ChildItem $desktopPackageRoot -Directory | Sort-Object { [version](($_.Name -split '-')[0]) } -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $coreRoot -or -not $desktopRoot) { throw 'Could not resolve self-contained runtime package folders.' }
Copy-Item (Join-Path $coreRoot 'LICENSE.TXT') (Join-Path $dist 'DOTNET_RUNTIME_LICENSE.txt') -Force
Copy-Item (Join-Path $coreRoot 'THIRD-PARTY-NOTICES.TXT') (Join-Path $dist 'DOTNET_RUNTIME_THIRD_PARTY_NOTICES.txt') -Force
Copy-Item (Join-Path $desktopRoot 'LICENSE') (Join-Path $dist 'DOTNET_WINDOWS_DESKTOP_LICENSE.txt') -Force

$sdkVersion = (& dotnet --version).Trim()
$desktopNotices = Join-Path $env:ProgramFiles "dotnet\sdk\$sdkVersion\Sdks\Microsoft.NET.Sdk.WindowsDesktop\THIRD-PARTY-NOTICES.TXT"
if (-not (Test-Path $desktopNotices)) { throw 'Windows Desktop third-party notices were not found.' }
Copy-Item $desktopNotices (Join-Path $dist 'DOTNET_WINDOWS_DESKTOP_THIRD_PARTY_NOTICES.txt') -Force

$appHash = (Get-FileHash -Algorithm SHA256 (Join-Path $dist 'STOW.exe')).Hash.ToLowerInvariant()
"$appHash  STOW.exe" | Set-Content (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii

@(
    "# STOW $version",
    "",
    "Technical preview build. Publishing is disabled until installer, migration, notices, signing, and release validation are complete."
) | Set-Content (Join-Path $dist 'release-notes.md') -Encoding utf8

$zip = Join-Path $dist "STOW-$version-win-x64.zip"
$zipInputs = Get-ChildItem $dist -File | Where-Object { $_.FullName -ne $zip } | Select-Object -ExpandProperty FullName
Compress-Archive -Path $zipInputs -DestinationPath $zip -CompressionLevel Optimal

Get-Item (Join-Path $dist 'STOW.exe'),$zip,(Join-Path $dist 'SHA256SUMS.txt') |
    Select-Object Name,Length,LastWriteTime
