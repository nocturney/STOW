$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'src\Trayify.cs'
$setupSrc = Join-Path $root 'src\TrayifySetup.cs'
$icon = Join-Path $root 'assets\Trayify.ico'
$dist = Join-Path $root 'dist'
$version = (Get-Content (Join-Path $root 'VERSION') -Raw).Trim()

if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must use semantic X.Y.Z format.' }

$sourceText = Get-Content $src -Raw
$setupText = Get-Content $setupSrc -Raw
if ($sourceText -notmatch ('VersionString\s*=\s*"' + [regex]::Escape($version) + '"')) { throw "Trayify.cs VersionString does not match VERSION=$version" }
if ($sourceText -notmatch ('AssemblyFileVersion\("' + [regex]::Escape($version) + '\.0"\)')) { throw "Trayify AssemblyFileVersion does not match VERSION=$version" }
if ($setupText -notmatch ('public const string Version\s*=\s*"' + [regex]::Escape($version) + '"')) { throw "TrayifySetup.cs Version does not match VERSION=$version" }
if ($setupText -notmatch ('AssemblyFileVersion\("' + [regex]::Escape($version) + '\.0"\)')) { throw "TrayifySetup AssemblyFileVersion does not match VERSION=$version" }

if (Test-Path $dist) { Get-ChildItem $dist -Force | Remove-Item -Force -Recurse } else { New-Item -ItemType Directory -Path $dist -Force | Out-Null }

$cscCandidates = @(
  "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
  "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw 'Could not find the .NET Framework C# compiler.' }

$app = Join-Path $dist 'Trayify.exe'
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /win32icon:$icon /out:$app /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $src
if ($LASTEXITCODE -ne 0) { throw "Trayify compilation failed with exit code $LASTEXITCODE" }

$setup = Join-Path $dist 'TrayifySetup.exe'
$resourceArg = '/resource:' + $app + ',Trayify.Payload.exe'
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /win32icon:$icon /out:$setup /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $resourceArg $setupSrc
if ($LASTEXITCODE -ne 0) { throw "TrayifySetup compilation failed with exit code $LASTEXITCODE" }

foreach ($file in @($app,$setup)) {
  $builtVersion = (Get-Item $file).VersionInfo.FileVersion
  if ($builtVersion -ne "$version.0") { throw "Built FileVersion $builtVersion does not match VERSION=$version for $file" }
}

$packageFiles = @(
  'README.md','CHANGELOG.md','LICENSE','PRIVACY.md','SECURITY.md','THIRD_PARTY_NOTICES.md'
)
foreach ($name in $packageFiles) { Copy-Item (Join-Path $root $name) (Join-Path $dist $name) -Force }

$appHash = (Get-FileHash -Algorithm SHA256 $app).Hash.ToLowerInvariant()
$setupHash = (Get-FileHash -Algorithm SHA256 $setup).Hash.ToLowerInvariant()
@(
  "$appHash  Trayify.exe",
  "$setupHash  TrayifySetup.exe"
) | Set-Content (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii

$changelog = Get-Content (Join-Path $root 'CHANGELOG.md') -Raw
$escaped = [regex]::Escape($version)
$match = [regex]::Match($changelog, "(?ms)^## \[$escaped\].*?(?=^## \[|\z)")
if (-not $match.Success) { throw "CHANGELOG.md has no section for $version" }
("# Trayify v$version`r`n`r`n" + $match.Value.Trim() + "`r`n") | Set-Content (Join-Path $dist 'release-notes.md') -Encoding utf8

$zip = Join-Path $dist "Trayify-v$version-win.zip"
$zipInputs = @($setup,$app) + ($packageFiles | ForEach-Object { Join-Path $dist $_ }) + @(Join-Path $dist 'SHA256SUMS.txt')
Compress-Archive -Path $zipInputs -DestinationPath $zip -CompressionLevel Optimal

Get-Item $app,$setup,$zip,(Join-Path $dist 'SHA256SUMS.txt'),(Join-Path $dist 'release-notes.md') | Select-Object Name,Length,LastWriteTime
