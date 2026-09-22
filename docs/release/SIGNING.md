# STOW Code Signing

STOW release binaries must use Authenticode code signing before the release workflow is considered final-release ready.
SHA-256 checksums protect download integrity but do not establish publisher trust or replace Authenticode.

## Required tooling

A signing machine or CI environment needs:

- a currently valid Code Signing certificate;
- access to the certificate private key;
- Code Signing EKU `1.3.6.1.5.5.7.3.3`;
- `signtool.exe` from the Windows SDK Signing Tools;
- network access to a trusted RFC 3161 timestamp server.

The current development machine was checked on 2026-09-22 and had neither a usable Code Signing certificate nor `signtool.exe`, so signing remains an explicit release blocker rather than being silently skipped.

## Signed build

Once a certificate and Signing Tools are available:

```powershell
$env:STOW_SIGNING_CERT_THUMBPRINT = '<thumbprint>'
powershell -ExecutionPolicy Bypass -File scripts\build-stow.ps1 -Sign
```

`build-stow.ps1 -Sign` signs `STOW.exe` and `STOWSetup.exe` before hashes and the ZIP are created. This ensures `SHA256SUMS.txt` describes the signed binaries that are actually released.

The signing script uses SHA-256 for file and timestamp digests and verifies each signature after signing.
The default timestamp endpoint is `https://timestamp.digicert.com`; it can be overridden when calling `scripts\sign-stow.ps1` directly.

## Verification

Run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\verify-release-signing.ps1
```

Both binaries must report Authenticode `Valid`. Unsigned binaries are allowed for local technical-preview testing but not for a final signed release gate.

## GitHub Actions release gate

The manual `STOW Release Gate` workflow expects two repository secrets:

- `STOW_SIGNING_PFX_BASE64` — the code-signing PFX encoded as Base64.
- `STOW_SIGNING_PFX_PASSWORD` — the PFX password.

The workflow imports the certificate only into the ephemeral runner's `CurrentUser\My` store, records its thumbprint through `GITHUB_ENV`, deletes the temporary PFX file, builds with `build-stow.ps1 -Sign`, verifies both Authenticode signatures, then uploads `dist-stow` as a short-lived workflow artifact.

The imported certificate is removed from the runner in an `always()` cleanup step. GitHub Release publication remains disabled; the workflow produces a signed release candidate only.
