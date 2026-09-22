param(
    [string]$Dist = (Join-Path (Split-Path -Parent $PSScriptRoot) 'dist-stow')
)

$ErrorActionPreference = 'Stop'
$files = @(
    (Join-Path $Dist 'STOW.exe'),
    (Join-Path $Dist 'STOWSetup.exe')
)

foreach ($file in $files) {
    if (-not (Test-Path $file)) { throw "Missing release binary: $file" }
    $signature = Get-AuthenticodeSignature $file
    if ($signature.Status -ne 'Valid') {
        throw "Authenticode signature is not valid for $file. Status: $($signature.Status)"
    }
    if (-not $signature.SignerCertificate) {
        throw "No signer certificate was reported for $file."
    }
}

Get-AuthenticodeSignature $files |
    Select-Object Path,Status,@{Name='Signer';Expression={$_.SignerCertificate.Subject}},
                  @{Name='Thumbprint';Expression={$_.SignerCertificate.Thumbprint}}
