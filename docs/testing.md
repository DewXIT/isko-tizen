# Verification ledger

Initial source snapshot: 2026-10-20. This repository contains no imported device
records, user test sessions, network topology or historical CI logs.

Compilation, portable C# checks, JS DOM doubles, host HTTP checks, device packaging
and real TV execution are different evidence. No TV test or deployment is performed
for this initial import. Hardware video/input/protection/lifecycle remain unverified.

## Native playback end to end (2026-10-21)

On the TV, with the fixture page and a locally served HLS stream: a script-driven press on "Play stream" made the page
request the manifest, the app detected it and started the native player without further input; the position advanced,
pause and resume worked, a right press seeked, and a Back press returned to the page, which stayed responsive. A photo
of the screen showed the full-screen test pattern with the control toast. Script-driven clicks, not remote presses.
A live-streaming site was also tried: its player requested no stream at all, on the TV or in a desktop browser, so
detection could not be exercised there. Real sites remain to be checked.

## Media decode capability on the TV (2026-10-21)

Unattended probes on the physical TV (a temporary harness, `tools/tv/device-harness-media.patch`, reporting to the
fixture server from the app side), repeated after a full power cycle of the TV:

- **Audio-only AAC (MP4/M4A) plays**: ready state 4, current time advancing.
- **VP9/Opus WebM plays** (software decode): video width 640, current time advanced through the clip, process alive.
- **H.264 does not play** through `<video>`: a local MP4 (main profile), an HTTPS MP4 and an HTTPS HLS stream fail with
  `MEDIA_ERR_SRC_NOT_SUPPORTED` (error 4, ready state 0), although `canPlayType` answers "probably"/"maybe". Switching
  the video path (`VideoHoleEnabled`) before loading, and declaring extra media privileges in the manifest, changed
  nothing. Media Source Extensions with H.264 killed the app process in one run. Protection on or off made no difference.
- A live-streaming site's player stayed hidden with nothing loaded, identically with protection on and off (7 requests
  blocked with it on). The earlier "audio plays, picture black or loader" symptom is consistent with this: audio decodes,
  H.264 video does not.
- **The native player decodes it.** `Tizen.Multimedia.Player` with `Display(NUI Window)`, transparent window, shell hidden:
  the same H.264 MP4 prepared as 1280x720 `h264`, played in real time to the end (12 s, playback-completed, process
  alive), and an HTTPS HLS stream (H.264) played continuously for 25 s. Only the `internet` privilege was declared. The
  platform decoder is therefore reachable from the app, but not through the WebView `<video>`: a transparent window plus
  `VideoHoleEnabled` still gave error 4. Whether the picture was visible on screen was not recorded by the harness.

## Request-path cost and lifecycle review (2026-10-20)

Desktop micro-benchmark of the per-request policy evaluation (400,000 mixed first-party, CDN and listed ad-host
URLs, starter list), **a proxy for work and garbage per request, not a TV measurement**:

| | time per request | allocated per request |
| --- | --- | --- |
| before (page and request parsed several times, substring suffix walk, array-based site key) | about 1.4 microseconds | 1065 bytes |
| after (page host remembered per page, request parsed once, hashed in-place host lookup, scan-based site key, lazy path) | about 1.0 microseconds | 195 bytes |

Timings on a shared machine vary between runs; the allocation figures are deterministic. Decisions are unchanged:
the protection tests (now with a differential check of the same-site rule over every pair of a host set, and a check
that the remembered page follows the page) pass. The remaining allocation is the single `Uri` parse per request.

Lifecycle review (code reading, no device memory trace): view teardown disposes whole subtrees and stops animations
before replacing them; page views detach every handler and the native request and download callbacks before
disposal; one-shot timers are rooted until they fire; history, favorites and the site-icon cache are bounded; icon
files are keyed by name and size only. Two growth risks were fixed: the overlay sweep re-examined every child of
`body` on each DOM mutation (it now examines only added nodes), and blocked-popup notices could queue without limit
(one pending notice at a time). Not covered: native memory held by the web engine, which needs the device samples in
the [checklist](tv-checklist.md).

## .NET 8 device target (2026-10-20)

`net8.0-tizen9.0` builds with the Samsung workload (framework-dependent package, `Microsoft.NETCore.App` 8.0.0).
On the physical TV it installed, but the process did not stay up and the fixture page was never requested, while the
`net6.0-tizen9.0` build of the same source runs. Conclusion: that TV has no usable .NET 8 runtime for this package.
The reference-compile and CI builds stay on net6.0; the end-of-life warning is silenced for that reason.

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

## CI

`.github/workflows/verify.yml` runs on every push and pull request (read-only token, 20 minute limit): the API12
reference build, the protection and core tests (including the hidden-member audit), the page-script, diagnostics and
packaging tests, the fixture server tests, a check that `tools/tv/device-harness.patch` still applies, the style and
hygiene check and the public-data gate. The shipped projects build with the .NET analyzers; the cleaned-up rules are errors. It does not run on a TV and never signs or installs anything; a green run is a portable check, not
a device result.

## Future device work

Use an explicitly authorized session and private SDK/signing/device configuration.
Test navigation/IME, trusted input and frames, player progress versus visible video,
fullscreen, protection positive/negative controls, close/hide/resume and long-running
memory trends separately. Do not publish serial numbers, network addresses,
certificate metadata, URLs, cookies, tokens or page/user text with results.
