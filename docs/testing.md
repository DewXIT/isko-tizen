# Verification ledger

Initial source snapshot: 2026-10-20. This repository contains no imported device
records, user test sessions, network topology or historical CI logs.

Compilation, portable C# checks, JS DOM doubles, host HTTP checks, device packaging
and real TV execution are different evidence. No TV test or deployment is performed
for this initial import. Hardware video/input/protection/lifecycle remain unverified.

## First on-device protection run (2026-10-20)

Physical TV, a private signed developer package built from `main` plus the changes listed here, and a
trusted-LAN fixture server. Evidence is **script-driven clicks through the app's own click path, plus the page's
own events and the app counters reported to the server**; not remote key presses, and no screen was observed for
most items.

- Request blocking: PASS. Baseline with protection off reached the server (`adRequests` 1); with protection and
  the fixture rule on the request did not (`requestsBlocked` 1) while ordinary links still arrived. A site
  exception let the request through again.
- Click trap over a button: the first scripted click reached the button (`player-click`, no `trap-click`).
- Interstitial (shadow DOM, maximum z-index) and trap: removed (`sweptLayers` 2); kept under a site exception.
- Popups: a cross-site `window.open` after a click and a `window.open` with no recent input were blocked
  (`popupsBlocked` 2, the page stayed); a same-site `window.open` after a click was allowed into the one view.
- Downloads: a plain archive link was refused (`downloadsBlocked` 1, `downloadsStarted` 0, page alive);
  `a[download]` clicks were swallowed by the page guard.
- Anti-adblock bait slots: 12 of 12 stayed visible.
- **Defect found and fixed:** unregistering the request interceptor while a page was live (protection off or a
  site exception) stalled every request of that page for minutes. It is now registered once per page and a
  disabled policy lets requests through. Repeating the scenarios after the change: all of the above passed.
- Not shown: remote-key behaviour, the on-screen keyboard, real pages, playback and memory. An earlier crash after
  typing an address by hand was not reproduced (the next attempt with the same input worked); its cause is unknown.

## Local verification

Executed on the standalone source tree:

- Default NUI reference build: PASS, with existing .NET 6 support warnings
  and the SDK XAML assembly notice.
- Core ownership/cursor/input model and compiled API12 audit: PASS; 186 Tizen
  references, 43 hidden members, all reviewed.
- Protection policy: PASS, 89 assertions.
- Page-script DOM doubles, packaging-plan/privacy/ZIP tests, diagnostic fallback
  and in-process fixture handlers: PASS.
- Real host-loopback HTTP fixture checks: PASS. Media range was explicitly skipped
  because the clean source distribution contains no generated MP4.
- No signed package, SDK credential access, TV connection or native execution was
  performed for this import. Generated output stays ignored, outside the commit.

Page-close memory release (tile resources, engine free-unused-memory, forced
managed-heap compaction on close): compiles and passes the hidden-API review. Its
effect on memory or smoothness is NOT measured; compare `after-close` samples on a
TV session before claiming any benefit.

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
