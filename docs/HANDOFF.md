# Development handoff

This repository starts with one independent source commit. It includes no prior
Git history, private installation configuration or recorded TV diagnostics.

Target: Tizen 9 / API12. `src/Isko.Tv` is the default; Lab shares its sources under
a separate ID. Legacy targets API11/Tizen 8 and is not the selected backend.

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
