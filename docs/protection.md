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

## Rule engine (source-level, not yet verified on a TV)

`FilterSet` parses a small original rule language (`||host^`, `||host/path*`, `@@` exceptions,
`$third-party`, simple `##selector` cosmetics). Unknown syntax is rejected, never guessed. The bundled
`StarterRules` list contains hosts of well-known ad networks only. Design rule: **breaking the page is worse
than showing an ad.**

- A request to the page's own site is never blocked, so a page's launcher and player scripts keep working.
  Site comparison is a heuristic without a public-suffix list; uncertain cases count as the same site.
- Bare TLD/public-suffix hosts, regex rules, request-type options and tag-only or player-like cosmetic
  selectors are rejected at parse time. The native interceptor reports only a URL, so type options cannot be honoured.
- Popups: blocked unless the user pressed OK within 2 s **and** the target is on the same site; the target
  still reuses the one view. Listed ad hosts and cross-site targets are always blocked. The site exception
  (menu) is the escape hatch. The native event carries no gesture metadata, so the app supplies its own.
- Click traps: a viewport-sized fixed/absolute layer with no content of its own (transparent, or at an extreme
  z-index) can swallow every click and open an ad. The remote cursor click is hit-tested with
  `elementsFromPoint` and skips such a layer, so the first press reaches the element under it. Layers with
  text, images, controls or an opaque background (consent dialogs, real modals) are never skipped, and any
  inspection failure means an ordinary click. Same-origin frames only; the `click-trap` fixture section
  reproduces the pattern. Checked with DOM doubles, not on a TV.
- Overlay sweep: a body-level layer that covers the viewport at the maximum z-index (also inside a shadow root),
  typical of ad interstitials and click traps, is hidden and removed, which also releases its decoder. A layer that
  holds a form, a first-party frame/video, is small, or sits at an ordinary z-index is kept. It is skipped during real
  fullscreen and when protection is off or the site has an exception. The count of removed layers is reported as
  `sweptLayers`. Checked with DOM doubles; the same script was also run once against a live ad-heavy page in a desktop
  browser, where it removed the interstitial and re-created layers. Not tested on a TV.
- Every blocked request increments a per-rule counter. Telemetry reports rule ids (list line numbers) and
  counts only, which locates a rule that broke a page without logging any URL.

Request, popup and cosmetic behavior is covered by portable policy tests only. Whether the native interceptor
blocks, and how the rules affect real pages, memory and playback, needs a TV session.

The original exact-host rules are pubads.g.doubleclick.net and
pagead2.googlesyndication.com. An optional /ad-probe rule is for controlled fixtures.
These rules are not a complete ad blocker, EasyList or a service-specific filter.
Server-side positive/negative controls, frames, cache and service-worker behavior
need separate native validation. No DRM/paywall bypass is implemented.
