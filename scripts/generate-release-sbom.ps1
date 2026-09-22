param(
    [string]$Dist = (Join-Path (Split-Path -Parent $PSScriptRoot) 'dist-stow')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$version = (Get-Content (Join-Path $root 'STOW_VERSION') -Raw).Trim()
$manifestPath = Join-Path $Dist 'LEGAL_MANIFEST.txt'

if (-not (Test-Path $manifestPath)) { throw 'LEGAL_MANIFEST.txt is required before SBOM generation.' }

$manifest = Get-Content $manifestPath
$coreLine = $manifest | Where-Object { $_ -like 'DOTNET_RUNTIME_PACK=*' } | Select-Object -First 1
$desktopLine = $manifest | Where-Object { $_ -like 'DOTNET_WINDOWS_DESKTOP_PACK=*' } | Select-Object -First 1
$d3dHashLine = $manifest | Where-Object { $_ -like 'WINDOWS_SDK_COMPONENT_SHA256=*' } | Select-Object -First 1
if (-not $coreLine -or -not $desktopLine -or -not $d3dHashLine) {
    throw 'Runtime pack or Windows SDK component identity is missing from LEGAL_MANIFEST.txt.'
}

$coreIdentity = $coreLine.Substring('DOTNET_RUNTIME_PACK='.Length)
$desktopIdentity = $desktopLine.Substring('DOTNET_WINDOWS_DESKTOP_PACK='.Length)
$d3dHash = $d3dHashLine.Substring('WINDOWS_SDK_COMPONENT_SHA256='.Length)
if ($d3dHash -notmatch '^[0-9a-fA-F]{64}$') { throw 'D3DCompiler SHA-256 in LEGAL_MANIFEST.txt is invalid.' }
$coreParts = $coreIdentity.Split('/')
$desktopParts = $desktopIdentity.Split('/')
if ($coreParts.Count -ne 2 -or $desktopParts.Count -ne 2) { throw 'Runtime pack identity format is invalid.' }

$appPath = Join-Path $Dist 'STOW.exe'
$setupPath = Join-Path $Dist 'STOWSetup.exe'
$legalPath = Join-Path $Dist 'STOW-Legal.zip'
foreach ($path in @($appPath,$setupPath,$legalPath)) {
    if (-not (Test-Path $path)) { throw "Missing SBOM artifact: $path" }
}

$appHash = (Get-FileHash $appPath -Algorithm SHA256).Hash.ToLowerInvariant()
$setupHash = (Get-FileHash $setupPath -Algorithm SHA256).Hash.ToLowerInvariant()
$legalHash = (Get-FileHash $legalPath -Algorithm SHA256).Hash.ToLowerInvariant()
$namespace = "https://github.com/nocturney/STOW/spdx/$version/$($appHash.Substring(0,16))"

$sbom = [ordered]@{
    spdxVersion = 'SPDX-2.3'
    dataLicense = 'CC0-1.0'
    SPDXID = 'SPDXRef-DOCUMENT'
    name = "STOW-$version-win-x64"
    documentNamespace = $namespace
    creationInfo = [ordered]@{
        created = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        creators = @('Organization: Christian Velvet','Tool: STOW build-stow.ps1')
    }

    packages = @(
        [ordered]@{
            name = 'STOW'
            SPDXID = 'SPDXRef-Package-STOW'
            versionInfo = $version
            downloadLocation = "https://github.com/nocturney/STOW/releases/tag/v$version"
            filesAnalyzed = $false
            licenseConcluded = 'MIT'
            licenseDeclared = 'MIT'
            copyrightText = 'Copyright (c) 2026 Christian Velvet'
            checksums = @(
                [ordered]@{ algorithm = 'SHA256'; checksumValue = $appHash }
            )
            comment = "STOW.exe SHA-256 $appHash; STOWSetup.exe SHA-256 $setupHash; STOW-Legal.zip SHA-256 $legalHash"
        },
        [ordered]@{
            name = $coreParts[0]
            SPDXID = 'SPDXRef-Package-DotNetRuntime'
            versionInfo = $coreParts[1]
            supplier = 'Organization: Microsoft Corporation'
            downloadLocation = 'NOASSERTION'
            filesAnalyzed = $false
            licenseConcluded = 'NOASSERTION'
            licenseDeclared = 'NOASSERTION'
            copyrightText = 'NOASSERTION'
            externalRefs = @(
                [ordered]@{
                    referenceCategory = 'PACKAGE-MANAGER'
                    referenceType = 'purl'
                    referenceLocator = "pkg:nuget/$($coreParts[0])@$($coreParts[1])"
                }
            )
            comment = 'Windows self-contained runtime component. See STOW-Legal.zip and LEGAL_MANIFEST.txt for applicable license material.'
        },
        [ordered]@{
            name = $desktopParts[0]
            SPDXID = 'SPDXRef-Package-WindowsDesktopRuntime'
            versionInfo = $desktopParts[1]
            supplier = 'Organization: Microsoft Corporation'
            downloadLocation = 'NOASSERTION'
            filesAnalyzed = $false
            licenseConcluded = 'NOASSERTION'
            licenseDeclared = 'NOASSERTION'
            copyrightText = 'NOASSERTION'
            externalRefs = @(
                [ordered]@{
                    referenceCategory = 'PACKAGE-MANAGER'
                    referenceType = 'purl'
                    referenceLocator = "pkg:nuget/$($desktopParts[0])@$($desktopParts[1])"
                }
            )
            comment = 'Windows Desktop/WPF runtime component. See STOW-Legal.zip and LEGAL_MANIFEST.txt for applicable license material.'
        },
        [ordered]@{
            name = 'D3DCompiler_47_cor3.dll'
            SPDXID = 'SPDXRef-Package-D3DCompiler'
            versionInfo = $desktopParts[1]
            supplier = 'Organization: Microsoft Corporation'
            downloadLocation = 'NOASSERTION'
            filesAnalyzed = $false
            licenseConcluded = 'NOASSERTION'
            licenseDeclared = 'NOASSERTION'
            copyrightText = 'NOASSERTION'
            checksums = @(
                [ordered]@{ algorithm = 'SHA256'; checksumValue = $d3dHash.ToLowerInvariant() }
            )
            comment = 'Sourced from the resolved Windows Desktop runtime pack. Microsoft .NET Windows licensing identifies this binary as governed by the Windows SDK License.'
        }
    )
    relationships = @(
        [ordered]@{
            spdxElementId = 'SPDXRef-DOCUMENT'
            relationshipType = 'DESCRIBES'
            relatedSpdxElement = 'SPDXRef-Package-STOW'
        },
        [ordered]@{
            spdxElementId = 'SPDXRef-Package-STOW'
            relationshipType = 'DEPENDS_ON'
            relatedSpdxElement = 'SPDXRef-Package-DotNetRuntime'
        },
        [ordered]@{
            spdxElementId = 'SPDXRef-Package-STOW'
            relationshipType = 'DEPENDS_ON'
            relatedSpdxElement = 'SPDXRef-Package-WindowsDesktopRuntime'
        },
        [ordered]@{
            spdxElementId = 'SPDXRef-Package-WindowsDesktopRuntime'
            relationshipType = 'CONTAINS'
            relatedSpdxElement = 'SPDXRef-Package-D3DCompiler'
        }
    )
}

$out = Join-Path $Dist 'SBOM.spdx.json'
$sbom | ConvertTo-Json -Depth 12 | Set-Content $out -Encoding utf8
Write-Output "Generated SPDX SBOM: $out"
