# Verification ledger

Initial source snapshot: 2026-10-20. This repository contains no imported device
records, user test sessions, network topology or historical CI logs.

Compilation, portable C# checks, JS DOM doubles, host HTTP checks, device packaging
and real TV execution are different evidence. No TV test or deployment is performed
for this initial import. Hardware video/input/protection/lifecycle remain unverified.

## Local verification

Executed on the standalone source tree:

- Default NUI and Lab reference builds: PASS, with existing .NET 6 support warnings
  and the SDK XAML assembly notice.
- Legacy API11 reference build: PASS, with existing deprecated-API warnings,
  .NET 6 support warnings and the SDK XAML assembly notice.
- Core ownership/cursor/input model and compiled API12 audit: PASS; 174 Tizen
  references, 34 reviewed hidden members.
- Protection policy: PASS, 41 assertions.
- Page-script DOM doubles, packaging-plan/privacy/ZIP tests, diagnostic fallback
  and in-process fixture handlers: PASS.
- Real host-loopback HTTP fixture checks: PASS. Media range was explicitly skipped
  because the clean source distribution contains no generated MP4.
- No signed package, SDK credential access, TV connection or native execution was
  performed for this import. Generated output stays ignored, outside the commit.

Source/test/tool code matches the reviewed baseline. Logo pixel payload is
unchanged; only non-pixel provenance metadata was removed. Public-data checks
cover staged source and root-commit contents before publishing.

## CI template

The repository includes tools/ci/verify.example.yml as an inactive template. To
enable it, use GitHub's web editor or a credential permitted to create workflows,
and copy it to .github/workflows/verify.yml. The source does not configure accounts
or change GitHub permissions.
New local page-input and packaging/privacy checks are included in that template.

## Future device work

Use an explicitly authorized session and private SDK/signing/device configuration.
Test navigation/IME, trusted input and frames, player progress versus visible video,
fullscreen, protection positive/negative controls, close/hide/resume and long-running
memory trends separately. Do not publish serial numbers, network addresses,
certificate metadata, URLs, cookies, tokens or page/user text with results.
