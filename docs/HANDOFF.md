# Development handoff

This repository starts with one independent source commit. It includes no prior
Git history, private installation configuration or recorded TV diagnostics.

Target: Tizen 9 / API12. `src/Isko.Tv` is the only application project. The former
Lab (separate app ID) and API11/Tizen 8 Legacy projects were removed; protection is
tested in the main app.

Product direction: [vision.md](vision.md). Primary audience: Poland, with Polish as
the default user-facing language. The core flow is open page → choose live/video
content → watch smoothly, using a simple remote-operated browser. Lightweight
operation and default-on ad/popup protection are central requirements. Describe
compatibility by browser behavior, not named target services. The current small
rule set is enabled by default but does not meet the desired uBlock-like coverage.

Current behavior: a native cursor moves without page JS; click/scroll scripts
use a bounded buffer and cannot grant trusted browser user activation. Media
observation is passive. A manual video-path toggle is guarded until page load.
One WebView is owned; close/hide disposes it, and resume requires reopening.

Playback (2026-10-21): the WebView cannot decode H.264 on the tested TV, so a streaming
manifest the page requests after the user presses OK is played by the TV's native player
(`Tizen.Multimedia.Player`); see [native-playback.md](native-playback.md). Verified on a test
stream and, by the owner, on a live-streaming site; stream addresses stay in memory only.
Protection (rule engine, popup policy, click-trap and overlay removal, download refusal)
was exercised on the TV with fixtures; see [protection.md](protection.md) and [testing.md](testing.md).

Outstanding work: header-dependent streams (the native player sends cookies but no Referer),
plain `<video src>` files, memory trends and long playback, remote-key behaviour, IME submit,
more real pages (the page-side filter list is small), and a header-adding local proxy if needed.
Device evidence comes from [tv-checklist.md](tv-checklist.md) and the harness patches in
`tools/tv/`. Do not connect to a TV without a new explicit request.

Use the commands and evidence boundaries in [testing.md](testing.md). Packaging
uses each installer's own ignored config and certificate profile; see
[install.md](install.md). Preserve origin-only persistence and redacted telemetry.
