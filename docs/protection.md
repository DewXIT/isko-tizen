# Protection prototype

Isko.Protection is original netstandard2.0 policy code with no downloaded filter
lists. Popup, navigation, HTTP resource and cosmetic decisions remain distinct.

Protection is enabled by default in both the policy and default native engine.
The [product goal](vision.md) is broader ad/popup protection that works without
initial setup and preserves playback. The current prototype does not meet that
goal; list sourcing/licensing/updates and wider filtering need further design.

- New windows are denied while enabled; accepted HTTP(S) targets reuse one view.
- Navigation rejects unsafe protocols and known ad-host destinations. Unknown
  gesture metadata is allowed rather than assumed to be an unwanted redirect.
- Resource rules match exact hosts and optional paths. The native adapter must
  actually intercept before download; a policy-only test is not blocking evidence.
- Cosmetic hiding does not prove a resource was prevented from downloading.
- Global disable/site exemptions unregister native request filtering when possible.

Callbacks use an immutable policy snapshot and one terminal request operation.
Ignore means continue normal resource loading; an empty 403 response attempts a
block. Failure does not trigger a second completion. Numeric counters distinguish
attempts, accepted operations and errors. Native callbacks do not touch NUI views.

App persistence accepts only safe HTTP(S) origins and excludes user-info, paths,
queries and fragments. Telemetry never includes callback URLs, cookie/header data,
exception messages or page text. Websites still have platform-managed storage.

The original exact-host rules are pubads.g.doubleclick.net and
pagead2.googlesyndication.com. An optional /ad-probe rule is for controlled fixtures.
These rules are not a complete ad blocker, EasyList or a service-specific filter.
Server-side positive/negative controls, frames, cache and service-worker behavior
need separate native validation. No DRM/paywall bypass is implemented.
