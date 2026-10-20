# Controlled developer fixtures

Requires Node 16+; original sample-media generation also needs FFmpeg with H.264/AAC.

```sh
node tools/fixture-server/generate-media.js
node tools/fixture-server/test-handlers.js
node tools/fixture-server/test.js
node tools/fixture-server/server.js
```

Servers bind 127.0.0.1 by default. An explicit trusted-LAN test can use:

```sh
ISKO_FIXTURE_HOST=0.0.0.0 node tools/fixture-server/server.js
```

Then open http://YOUR_COMPUTER_LAN_IP:8765/ on your own device. Stop the server
when finished. It is unauthenticated development tooling, not part of the TV app.
The two fixture origins share controlled run counters. Different ports alone do
not mean different domains; ISKO_CROSS_ORIGIN can select a plain HTTP(S) origin.

The generated 12-second test pattern and audio are original, ignored by Git and
reproducible. No downloaded content, user recordings or device measurements are
included. HTTP range success and file metadata do not prove native decoding.

Separate cases cover ordinary links, popup/target blank, navigation, cosmetic
visibility versus request counters, frame boundaries, fullscreen, source switching
and cleanup. A blocked-resource zero count needs a positive unblocked control.
Use independent run IDs, bounded numeric counters and no browsing credentials.
Hour-long playback and repeated lifecycle tests require future device evidence.

Protection scenarios added for the TV session ([checklist](tv-checklist.md)): a transparent full-viewport click
trap over a button, a shadow-DOM interstitial at the maximum z-index, `a[download]`/plain/scripted downloads of an
empty archive (`/download/sample.zip`, counted per run as `downloads`), ad-like bait slots that must not be hidden,
and a cross-site `window.open` from a cross-origin frame. The cross-site target is a different host name
(`localhost` versus `127.0.0.1`, or `ISKO_CROSS_ORIGIN`). The server counters show requests that arrived;
only the app's own counters show what it refused. Browser-side behavior of these pages was checked with the app's
bundled scripts in a desktop browser, not in the TV WebView.
