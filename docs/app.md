# Native browser architecture

The default is `Tizen.NUI.BaseComponents.WebView`, pinned to API12 12.0.0.18510.
The desktop/reference target is `net6.0`; `BuildForTizen=true` selects
`net6.0-tizen9.0` and a TPK. Reference DLLs cannot run this native UI on desktop.
See [install.md](install.md) for local signing and configuration.

## Shell and page ownership

The home screen shows address/search, history/favorites and saved-origin tiles.
Pages fill the window; an on-demand bar and side menu provide address, reload,
history Back, favorites and close. Shell layers have explicit draw order.

A single WebView is owned. A new explicit address disposes its predecessor before
creating another. Failed cleanup retains ownership and prevents another allocation.
Pause/hide releases the page; resume does not silently reload or resume playback.
History/favorites store origins only, not stream paths, queries or fragments.
Cookies and site storage are the platform's separate responsibility.

## Input and lifecycle

Remote arrows move an accelerated native cursor; OK submits a coordinate-preserving
JS click. At an edge arrows request coalesced scrolling. At most one input script
runs with bounded pending work. A busy callback does not grow an unbounded queue;
late results are tied to their generation. Opening browser chrome discards pending
input while preserving an outstanding native call.

The script events remain untrusted. Cross-origin iframe contents and closed shadow
roots are inaccessible. Native fullscreen/user-activation behavior is not implied
by synthetic input. Address entry has a separate submit row and URL keyboard.

Back handles shell overlays, then HTML fullscreen and engine history, then shows
Close in the bar. Unknown or timed-out engine state is reported rather than assumed.
The native callback ordering, IME and key delivery need device tests.

## Media and protection

Site players own playback/preview handoffs. Observers count errors/waiting/stalls
without pausing other players. Explicit view destruction remains the release path.
Numeric telemetry selects active video and records readiness, buffer and geometry
without media-source URLs. A page-menu switch changes VideoHoleEnabled only after
a completed load; it is a diagnostic option, not a guaranteed rendering fix.

The default protection adapter connects reviewed policy, request, history and
fullscreen hooks. It has only minimal exact-host rules. Disabled/exempt request
filtering is unregistered. Single-completion and metadata tests do not establish
native blocking effectiveness. See [protection.md](protection.md).
