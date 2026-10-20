# Device session checklist (protection)

Nothing here has run on a TV yet. Do not read a local pass as a TV pass: the script-level behavior below
was checked in a desktop browser against the fixtures, and the policy code with portable tests only.
Use a build without the inspector ([install](install.md)) and the fixture server on a trusted LAN
([fixtures](fixtures.md)). Open the fixture index at the computer's IP; for cross-site cases the cross-site
popup target uses a different host name (`localhost`/`127.0.0.1` swap, or `ISKO_CROSS_ORIGIN`), so use a
host name the TV can resolve if the swap is not reachable from the TV.

Record per row: what happened on screen, the app's JSON counters (`kind: "memory"` lines: `popupsBlocked`,
`navigationsBlocked`, `requestsBlocked`, `requestBlockAttempts`, `downloadsBlocked`, `downloadsStarted`,
`requestBlocking`; media samples: `sweptLayers`; `rule-hit-<id>` events) and the server counters
(`/api/counters?run=...`). Use a new run ID per row and press the fixture's Reset first.

| # | Setup | Action | Pass when |
| - | ----- | ------ | --------- |
| 1 | Protection **off** | Request `/ad-probe`; arm trap and interstitial; fixture download link | Server `adRequests` = 1; trap and interstitial stay; baseline established |
| 2 | Protection on, "Test blokady żądań" on | Request `/ad-probe` | Server `adRequests` stays 0 while the control request still works; `requestsBlocked` = 1. If `requestBlocking` reads `hook-toggle-failed` or `unverified` after use, interception is not working |
| 3 | Protection on | Arm click trap, press OK over the player button once | Fixture log shows `player-click`, not `trap-click`, on the first press |
| 4 | Protection on | Arm interstitial (shadow DOM, max z-index) | It disappears within a second; `sweptLayers` increases; the page stays usable |
| 5 | Protection on | Same-site `window.open` button right after pressing OK | Allowed; opens in the one view (no second view) |
| 6 | Protection on | Cross-origin frame: "Cross-site popup" | Blocked: `popupsBlocked` +1, nothing navigates |
| 7 | Protection on | "window.open after 5 s": press it once, then do not press OK again | Blocked (no recent input): `popupsBlocked` +1; the fixture log shows the attempt |
| 8 | Protection on | `a[download]`, plain archive link, scripted download | Nothing is saved; `downloadsBlocked` +1 for the plain link; `downloadsStarted` stays 0 (non-zero = refusal did not hold) |
| 9 | Protection on | Insert bait slots | Log shows only `bait-visible` (never `bait-hidden`) |
| 10 | Site exception on | Repeat 2, 4, 6 | Everything is allowed again (the exception works as the escape hatch) |
| 11 | Protection on | Play the local video 10 minutes | No new blocks, no stalls caused by the filters; note memory samples |

Private real-page checks (owner only; results stay out of the repository, described by behavior class):
first click reaches the content list, a stream opens with the first press, no new window or download appears,
audio and picture start together. Compare with protection off and with the system browser: time to first
picture, remote latency, memory samples after 10/30/60 minutes. Note anything that stopped working because of
a rule (`rule-hit-<id>` events identify the rule by its line in the starter list).

## Unattended run (no remote)

`node tools/tv/package.cjs --install --start-url "http://HOST:8765/?report=1&run=auto-01"` builds a **private
developer package** that opens that address at launch (the address is embedded, so it fails the publication audit
only for the LAN-address rule and must not be shared). With the fixture server on a trusted LAN, the fixture page
reports its own events to `/api/events?run=...`. `tools/tv/device-harness.patch` (apply with `git apply`, never
commit the result) adds a scripted sequence that drives rows 2 to 9 through the app's own click path (script clicks,
not remote key presses), and reports the app counters to the server. Liveness is checked by re-running
`tz run --package-id ... --serial ...`: the same pid means the process did not restart.
