# Working on Isko Tizen

Read docs/HANDOFF.md and docs/testing.md before changing the project.

- Target Tizen 9.0, with pinned API12 reference assemblies. Model year is not an
  API version. Do not require a device readout before ordinary source work.
- Do not connect to a TV, install packages or set up an emulator unless the user
  explicitly requests it. Local/reference checks are independent of TV tests.
- SDK members marked EditorBrowsable(Never) may be called directly only when the
  selected API12 SDK exposes them without extra privileges beyond the manifest.
  Review each member and update the metadata audit in tests/Isko.Core.Tests.
  Do not use reflection, P/Invoke or copied platform DLLs as workarounds.
- Keep a single application-owned WebView, origin-only app persistence and numeric,
  redacted telemetry. Preserve the site's player and normal browser security rules.
- Use the existing .NET/Node checks. Keep SDKs, credentials, pairing material,
  device identifiers, private configuration and generated packages out of Git.
- Compilation, DOM doubles, host HTTP checks and native TV execution are separate
  evidence. Do not label a local pass as a TV or playback pass.
