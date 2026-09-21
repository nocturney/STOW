$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sessionId = (Get-Process -Id $PID).SessionId
if ($sessionId -eq 0) { throw 'Live parity requires an interactive Windows desktop session.' }

$project = Join-Path $root 'tests\STOW.LiveParityHarness\STOW.LiveParityHarness.csproj'
$result = Join-Path $root 'artifacts\live-parity-harness.log'
$exe = Join-Path $root 'tests\STOW.LiveParityHarness\bin\Release\net10.0-windows\STOW.LiveParityHarness.exe'

& dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Path (Split-Path -Parent $result) -Force | Out-Null
Remove-Item $result -Force -ErrorAction SilentlyContinue
& $exe --result $result
$code = $LASTEXITCODE
Get-Content $result
exit $code
