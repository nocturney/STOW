$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'src\Trayify.cs'
$icon = Join-Path $root 'assets\Trayify.ico'
$dist = Join-Path $root 'dist'
$out = Join-Path $dist 'Trayify.exe'

New-Item -ItemType Directory -Path $dist -Force | Out-Null

$cscCandidates = @(
  "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
  "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
  throw 'Could not find the .NET Framework C# compiler.'
}

& $csc /nologo /target:winexe /platform:anycpu /optimize+ /win32icon:$icon /out:$out /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $src
if ($LASTEXITCODE -ne 0) {
  throw "Compilation failed with exit code $LASTEXITCODE"
}

Copy-Item (Join-Path $root 'Install-Trayify.cmd') (Join-Path $dist 'Install-Trayify.cmd') -Force
Copy-Item (Join-Path $root 'README.md') (Join-Path $dist 'README.md') -Force
Copy-Item (Join-Path $root 'CHANGELOG.md') (Join-Path $dist 'CHANGELOG.md') -Force

$zip = Join-Path $dist 'Trayify-v0.1.0-win.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $out,(Join-Path $dist 'Install-Trayify.cmd'),(Join-Path $dist 'README.md'),(Join-Path $dist 'CHANGELOG.md') -DestinationPath $zip

$hash = (Get-FileHash -Algorithm SHA256 $out).Hash.ToLowerInvariant()
"Trayify.exe  $hash" | Set-Content (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii

Get-Item $out,$zip,(Join-Path $dist 'SHA256SUMS.txt') | Select-Object Name,Length,LastWriteTime
