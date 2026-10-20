# Changelog

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
