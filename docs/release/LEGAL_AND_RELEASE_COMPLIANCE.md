# STOW legal, licensing and release compliance

This document is an engineering compliance checklist, not legal advice. It records what STOW can verify automatically and what still requires human/legal review before circumstances change.

## Automated release gates

Every release build must:

- preserve the STOW MIT License and copyright notice;
- include Privacy, Security, third-party notices and credits;
- resolve the exact .NET runtime packs used for the self-contained Windows build;
- copy the exact runtime-pack license and third-party notice files;
- include offline snapshots/references for the Microsoft .NET Library License and Windows SDK License relevant to Windows runtime/WPF components;
- generate `LEGAL_MANIFEST.txt` with exact runtime pack versions and SHA-256 values;
- bundle the legal material in the application assembly, the installer legal bundle and the release ZIP;
- publish `STOW-Legal.zip` beside direct executable/installer release assets;
- fail if a new runtime `PackageReference` appears without an explicit review;
- fail if a new bundled font/image/icon asset appears without provenance/attribution review;
- verify the installer actually deploys the legal bundle;
- require explicit license/third-party-term acknowledgment for interactive installation and for first-time silent installation;
- require the application itself to enforce the current legal-terms revision on first run so portable, WinGet, Scoop and upgrades from pre-acceptance builds cannot bypass acknowledgment;
- preserve package-manager ownership for package-managed copies;
- keep the Privacy notice aligned with actual network/data behavior.

The automated gate is `scripts/verify-release-legal.ps1`.

Accessibility is a separate release-quality gate rather than a legal certification claim. UI-changing releases must complete the acceptance checks in `docs/accessibility/ACCESSIBILITY.md`, including keyboard, UI Automation, 200% text, contrast-theme and live restore-parity checks.

## Contribution provenance

External contributions are accepted under the repository MIT License. Contributors must have the right to submit their work and must not import third-party material without compatible licensing and required attribution.

No separate CLA is currently required by STOW.

## Privacy and user control

STOW is local-first and has no first-party analytics, advertising, telemetry or cloud account.

Network requests are user initiated for update checks and release-note retrieval. Diagnostics are local and copied to the clipboard only.

The uninstaller can delete the local STOW data directory when the user selects the remove-settings option.

Windows/.NET operating-system services such as Windows Error Reporting or security reputation checks are outside STOW and are disclosed separately in the Privacy notice.

## Security

GitHub Private Vulnerability Reporting must remain enabled for the repository.

Preview releases may remain unsigned under the current release policy, with an explicit SmartScreen/publisher-trust warning. Stable publication remains gated on Authenticode signing unless that policy is deliberately changed after review.

## Manual review required before Stable/commercial changes

Automation cannot establish the following:

- trademark/name clearance for **STOW** in every market or product class; see `TRADEMARK_SCREENING.md` for the preliminary screen and the explicit pre-Stable clearance gate;
- local consumer-law, warranty, refund or mandatory disclosure requirements if STOW is sold or bundled commercially;
- tax/VAT obligations arising from paid distribution;
- sanctions/export-control obligations that may depend on distribution model, cryptography, users or jurisdictions;
- organization-specific privacy/data-protection obligations if telemetry, accounts, cloud sync or personal-data processing are later introduced;
- third-party platform/store contractual terms for future Microsoft Store or other marketplace distribution.

Before a paid, enterprise-contract or broadly marketed Stable launch, obtain qualified legal review for the relevant jurisdictions and business model.

## Review triggers

Repeat the legal/licensing audit whenever any of the following changes:

- runtime/framework version or deployment model;
- new NuGet/runtime dependency;
- bundled font, icon, image, model, copied code or binary;
- telemetry, crash reporting, cloud sync, account or analytics feature;
- distribution through a new marketplace/package service;
- pricing or commercial terms;
- publisher/company identity;
- product name/branding;
- code-signing model.
