param(
    [Parameter(Mandatory = $true)][string]$Dist,
    [Parameter(Mandatory = $true)][string]$CertificateThumbprint,
    [string]$TimestampUrl = 'https://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$thumbprint = ($CertificateThumbprint -replace '\s','').ToUpperInvariant()

function Find-SignTool {
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $roots = @(
        "$env:ProgramFiles(x86)\Windows Kits\10\bin",
        "$env:ProgramFiles\Windows Kits\10\bin"
    ) | Where-Object { $_ -and (Test-Path $_) }

    $candidates = foreach ($root in $roots) {
        Get-ChildItem $root -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' }
    }
    return $candidates | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}

$cert = Get-ChildItem Cert:\CurrentUser\My,Cert:\LocalMachine\My -ErrorAction SilentlyContinue |
    Where-Object { $_.Thumbprint -eq $thumbprint } |
    Select-Object -First 1
if (-not $cert) { throw "Code-signing certificate $thumbprint was not found." }
if (-not $cert.HasPrivateKey) { throw 'The selected certificate has no private key.' }
if (-not ($cert.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3')) {
    throw 'The selected certificate is not valid for Code Signing.'
}
if ($cert.NotBefore -gt (Get-Date) -or $cert.NotAfter -lt (Get-Date)) {
    throw 'The selected code-signing certificate is not currently valid.'
}

$signtool = Find-SignTool
if (-not $signtool) {
    throw 'signtool.exe was not found. Install the Windows SDK Signing Tools before creating a signed release.'
}

$machineStore = $cert.PSParentPath -like '*LocalMachine*'
$files = @(
    (Join-Path $Dist 'STOW.exe'),
    (Join-Path $Dist 'STOWSetup.exe')
)
foreach ($file in $files) {
    if (-not (Test-Path $file)) { throw "Missing release binary: $file" }
    $args = @('sign','/fd','SHA256','/td','SHA256','/tr',$TimestampUrl,'/sha1',$thumbprint)
    if ($machineStore) { $args += '/sm' }
    $args += $file
    & $signtool @args
    if ($LASTEXITCODE -ne 0) { throw "Signing failed for $file" }
}

foreach ($file in $files) {
    & $signtool verify /pa /v $file
    if ($LASTEXITCODE -ne 0) { throw "Signature verification failed for $file" }
}

Get-AuthenticodeSignature $files |
    Select-Object Path,Status,StatusMessage,SignerCertificate,TimeStamperCertificate
