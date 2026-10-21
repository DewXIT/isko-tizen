# Working on Isko Tizen

Read docs/HANDOFF.md and docs/testing.md before changing the project.

- Product priority: a simple, lightweight browser for a Polish audience. Open a
  page, select a live stream/video and watch smoothly with a TV remote. Read
  docs/vision.md. Responsiveness, stable playback and default-on ad/popup protection
  take priority over adding browser features. No named sites are product targets.
- uBlock-like protection is a desired outcome, not a claim about the current small
  rule set. Do not present it as implemented or verified without evidence.
- Target Tizen 9.0, with pinned API12 reference assemblies. Model year is not an
  API version. Do not require a device readout before ordinary source work.
- Do not connect to a TV, install packages or set up an emulator unless the user
  explicitly requests it. Local/reference checks are independent of TV tests.
- SDK members marked EditorBrowsable(Never) may be called directly only when the
  selected API12 SDK exposes them without extra privileges beyond the manifest.
  Review each member and update the metadata audit in tests/Isko.Core.Tests.
  Do not use reflection, P/Invoke or copied platform DLLs as workarounds.
- Keep a single application-owned WebView, origin-only app persistence and numeric,
  redacted telemetry. Preserve normal browser security rules.
- Owner decision (2026-10-21): the WebView cannot decode H.264 on the target TV, while the
  platform's native player can. A streaming manifest (HLS/DASH) the page requests is therefore
  played by the native player (docs/native-playback.md). Stream addresses carry tokens: keep them
  in memory only, never log or persist them. No DRM or paywall bypass.
- Use the existing .NET/Node checks. Keep SDKs, credentials, pairing material,
  device identifiers, private configuration and generated packages out of Git.
- Compilation, DOM doubles, host HTTP checks and native TV execution are separate
  evidence. Do not label a local pass as a TV or playback pass.
