# Native playback

On the tested Tizen 9 TV the NUI WebView cannot decode H.264: a `<video>` with an H.264 MP4 or HLS stream fails with
`MEDIA_ERR_SRC_NOT_SUPPORTED`, although audio and VP9 work ([evidence](testing.md)). The platform's own player,
`Tizen.Multimedia.Player`, decodes the same streams in hardware and draws into the application window. Isko therefore
hands the stream a page asks for to that player.

## How it works

1. The request interceptor sees every request the page makes. A URL whose path ends in `.m3u8` or `.mpd` that the
   protection policy lets through is a stream candidate (`StreamUrls`). Repeats of the same playlist are ignored.
2. If the user pressed OK within the last 30 seconds, the candidate is played at once: the page is paused and hidden, the
   window becomes transparent, the shell is hidden and the native player starts. A candidate without a recent press is
   only offered: a toast and the page menu item "Odtwórz wykrytą transmisję".
3. Once a page has asked for a manifest, the WebView's requests for media segments (`.ts`, `.m4s`, `.aac`, `.mp4`...) are
   refused: the WebView cannot decode H.264, and feeding it segments crashed the app in a probe. The native player
   fetches the stream itself.
4. During playback: OK pauses or resumes, left/right seek by 10 s (when the stream has a duration; live streams are not
   seekable), Back or Exit returns to the page. The end of the stream and player errors return to the page as well.

The stream address often carries a session token. It is kept in memory only: never logged, never persisted, not part of
telemetry (only the host, via `StreamUrls.Describe`, may appear in diagnostics).

## Limits and open questions

- The native player is given the page's cookies (`document.cookie`, so not HttpOnly ones) but no `Referer` or `Origin`.
  A stream that requires them will fail to load; the shell then returns to the page. A header-adding local proxy is the
  next step if real sites need it.
- Detection covers manifests only. A plain `<video src="...mp4">` is not handed over yet.
- Verified on the TV with a locally served HLS stream (detection after a click, start, pause, resume, seek, return). Not
  yet verified on real sites; one live-streaming site's player never requested a stream in either the TV or a desktop
  browser during the test, so it could not be used.
- Ads inside the page's own player are bypassed because the player is not used, and ad hosts stay blocked by the filter
  list. Nothing here circumvents DRM or access controls: a stream the page could not request is not played.
- This changes the earlier rule of preserving the site's player; see the owner decision in `AGENTS.md`.
