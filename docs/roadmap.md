# Roadmap

Isko is an early alpha targeting Tizen 9 / NUI WebView. Priorities below describe
work to investigate and verify, not promised features or delivery dates.

The [product direction](vision.md) is open page → choose live/video content → watch
smoothly. Keep the browser simple and lightweight for a primarily Polish audience.
Default-on ad/popup protection is a core requirement; the current small rule set
does not meet the intended uBlock-like coverage. No particular services define
the public compatibility scope.

## First priorities

| Area | Next useful outcome | Evidence needed |
| --- | --- | --- |
| Video | Native playback on real pages: streams that need a Referer or Origin header, plain `<video>` files, live seeking and long sessions. | Device runs on real pages, memory samples over time, comparison with the system browser. |
| Remote input | Reliable address submission, pointer clicks and return to browser controls. | IME and key behavior on real remotes; trusted-input and frame limits documented. |
| Performance | Identify costly input dispatch, page work and native view overhead. | Reproducible measurements with protection on/off and bounded input load. |
| Lifecycle | Safe native callbacks and cleanup across close, hide and reopen. | Failure cases, repeated cycles and long playback/memory observations. |
| Protection | Design broader default-on ad/popup protection, including filter sourcing, updates, cosmetic rules and performance cost. | Positive/negative controls for requests, popups, navigation and frames, with working playback and site exceptions. |

Native work requires an explicitly authorized TV session using the tester's own
private configuration. Portable tests alone cannot close these items.

## Ways to help without a TV

- Improve setup instructions and explain toolchain failures using redacted examples.
- Keep Polish as the primary UI language and plan optional English localization.
- Improve fixture scenarios for nested frames, fullscreen and late callbacks.
- Review policy edge cases and add tests for concrete failures.
- Review the public-data gate and packaging helper with synthetic inputs.

Discuss larger changes in [Issues](https://github.com/DewXIT/isko-tizen/issues/new/choose).
See [CONTRIBUTING.md](../CONTRIBUTING.md) for the workflow and
[testing.md](testing.md) for what has actually been verified.

## Alpha release policy

Initial public source snapshot: **v0.1.0-alpha.1**. Alpha releases are source-only.
Users build and sign for their own TV; no shared certificate or device-specific
TPK is distributed. Future stable releases need a documented compatibility scope,
repeatable installation and device evidence for core input/playback/lifecycle behavior.
