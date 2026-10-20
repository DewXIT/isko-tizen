# Changelog

## Unreleased

### Added

- Filter rule engine with a bundled starter list of ad-network hosts; requests to a page's own site are never blocked.
- Popup policy driven by the user's last remote press: same-site only, listed or cross-site targets blocked.
- Removal of full-viewport maximum-z-index layers (ad interstitials, click traps) and click-through for the remote cursor.
- Refusal of file downloads (engine response policy plus a page-side guard), independent of the protection switch.
- Back slot on the control bar becomes Close when there is no history.
- Developer-only `--start-url` build option and fixture event channel for unattended device runs; device session checklist.

### Changed

- The `Verify` GitHub Actions workflow is now active (portable checks only).

### Fixed

- The request interceptor is registered once per page. Unregistering it on a live page (protection off or a site exception) stalled the page on a TV.
- `tools/tv/package.cjs --install` no longer fails on a shadowed variable.

### Removed

- The separate Lab and Legacy (API11) projects.

## v0.1.0-alpha.1 — 2026-10-20

First public alpha source snapshot for Tizen 9 / API12.

### Included

- C#/.NET NUI browser shell with home, address/search, favorites, history and
  on-demand controls.
- Native remote cursor and bounded page click/scroll dispatch.
- Website-player integration, fullscreen hooks and a manual video-path option.
- Experimental popup/navigation/request protection and session exceptions.
- Local build/sign/install helper using each user's own private configuration.
- Portable C#/Node checks, controlled host fixtures and a compiled SDK API audit.
- English/Polish project introduction, contribution/security guides and issue templates.

### Known limits

- Audio with a black video/loader remains unresolved. Streaming-site compatibility,
  IME, native callbacks, fullscreen and long playback need device evidence.
- Synthetic clicks cannot grant trusted browser user activation.
- The interface is primarily Polish. Protection is limited and experimental.
- Distribution is source-only; no universal or device-signed installer is provided.
- Reference builds and portable checks pass. The CI workflow remains an inactive
  template; this release does not claim a new TV test or deployment.

See [installation](docs/install.md), [verification](docs/testing.md) and
[roadmap](docs/roadmap.md).
