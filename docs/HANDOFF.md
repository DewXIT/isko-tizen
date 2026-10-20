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

Outstanding work: native callback lifetime and failure recovery, IME submit,
synthetic-input limitations, cross-origin players, audio without video/loader,
fullscreen, protection effectiveness, memory trends and long playback. These need
separate device evidence. Do not connect to a TV without a new explicit request.

Use the commands and evidence boundaries in [testing.md](testing.md). Packaging
uses each installer's own ignored config and certificate profile; see
[install.md](install.md). Preserve origin-only persistence and redacted telemetry.
