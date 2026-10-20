# Browser and video compatibility

This is an implementation review, not a record of any person's TV or network.
The project has no bundled device measurements, channel URLs or user screenshots.

## Comparison with a system TV browser

| Area | Isko implementation | Limit to validate |
| --- | --- | --- |
| Remote pointer | Native cursor; JS hit testing/click/scroll | Synthetic events do not grant trusted browser user activation. |
| Frames | Accessible document, same-origin frames and open shadow roots | Cross-origin players and closed roots cannot receive injected input. |
| Address entry | URL keyboard, validation, search and explicit submit row | Platform IME Go/Enter handling needs native testing. |
| Back/fullscreen | Overlay state, HTML fullscreen query, engine history, Close | Timeout/native callback behavior and fullscreen activation require device evidence. |
| Tabs/resume | One owned view; page disposed on hide/close | No tabs or automatic restoration of transient player state. |
| Persistence | Origins only for app history/favorites | Does not restore an exact stream URL; site cookies/storage remain platform-managed. |
| Video | Preserve site player; passive media observation | MSE, codecs, DRM, site policy and hardware rendering remain engine/device dependent. |
| Protection | Two minimal exact-host rules and popup/navigation policy | Not a complete ad blocker and not proof of pre-download cancellation. |

A click with coordinates is still not a trusted native click. Therefore an event
handler executing does not prove that autoplay, fullscreen or another gated API
will work. A full-window page is also distinct from HTML video fullscreen.

## Audio with a loader or black picture

The cause is unresolved. Possible layers to distinguish are site player state,
buffering, video decode and hardware-plane composition. Audio, readyState=4 and a
successful fullscreen request alone do not prove visible frames. A WebView content
screenshot may omit separately composed hardware video.

The bundled observer does not pause sibling media on play. This avoids interfering
with preview/live handoffs. Click scripts preserve hit coordinates; native input
queues are bounded. These changes do not certify video compatibility.

For a future explicitly authorized device test, compare the same content/quality
and page phase in Isko and the system browser. Record numeric state, progress,
frames and visible output separately. Compare the two video rendering paths after
load without replacing the source. Keep startup, channel switching, steady playback,
fullscreen, Back and hide/resume as separate cases. Do not derive a speedup from
different channels or phases. Do not log full URLs, cookies or user text.

Reference documentation:
- [Samsung browser remote interaction](https://www.samsung.com/de/support/tv-audio-video/wie-surfe-ich-mit-meinem-qled-smart-tv-im-internet/)
- [LG browser overview](https://www.lg.com/us/support/help-library/lg-tv-how-to-use-the-web-browser-CT10000018-20153257222107)
- [HTML user activation](https://html.spec.whatwg.org/multipage/interaction.html#tracking-user-activation)
- [Samsung HTML5 video](https://developer.samsung.com/smarttv/develop/guides/multimedia/media-playback/using-video-elements.html)
- [TizenFX API12 WebView](https://github.com/Samsung/TizenFX/blob/API12/src/Tizen.NUI/src/public/WebView/WebView.cs)
