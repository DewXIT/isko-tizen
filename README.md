# Isko Tizen

<img src="src/Isko.Tv/shared/res/isko.png" alt="Isko logo" width="144" />

A lightweight Samsung TV browser prototype targeting **Tizen 9 / NUI WebView**.
MIT licensed. Browse with a remote-controlled cursor and use the website's player.

The prototype has native address/search entry, home/history/favorites, an on-demand
control bar, bounded page input and minimal popup/request policies. It is not a
finished ad blocker. Video compatibility, trusted input, fullscreen and long-running
playback require device validation; audio with a black video/loader remains an
unresolved compatibility issue. No TV data or device test recordings are included.

## Build and install for your own TV

Install Node and a .NET/Samsung toolchain supporting `net6.0-tizen9.0`. Create your
own Samsung TV signing profile for your own device. Configure a private local copy:

```sh
mkdir -p .local
cp tools/tv/config.example.json .local/isko-tv.json
# Edit only the local copy: tool paths, your signing profile, optional TV address.
node tools/tv/package.cjs --dry-run
node tools/tv/package.cjs
# Explicitly opt in to connecting, installing and launching:
node tools/tv/package.cjs --install
```

Packaging does not contact a TV without `--install`. Normal output disables the
runtime inspector and debug symbols, remaps source paths and audits package
contents. Private configuration, keys, profiles and output are ignored by Git.
Each installer signs locally; a device-specific signed TPK should remain private.
See [installation](docs/install.md).

## Local verification

```sh
dotnet build src/Isko.Tv/Isko.Tv.csproj -c Release
dotnet build src/Isko.Tv.Experimental/Isko.Tv.Experimental.csproj -c Release
dotnet run --project tests/Isko.Core.Tests -c Release -- src/Isko.Tv/bin/Release/net6.0/Isko.Tv.dll
dotnet run --project tests/Isko.Protection.Tests -c Release
node tests/page-scripts.test.js
node tests/packaging.test.cjs
node tests/diagnostics.test.js
node tools/check-public-data.cjs
node tools/fixture-server/test-handlers.js
```

These checks do not run the native application or prove video playback. The
`net6.0` target is reference compilation only. Samsung's device target and SDK
versions remain separate requirements. Existing .NET 6 support warnings are visible.

## Project layout

- `src/Isko.Tv`: default browser shell and reviewed API12 engine hooks.
- `src/Isko.Core`: view ownership, shell state, cursor and input buffer.
- `src/Isko.Protection`: minimal policy and request-completion adapters.
- `src/Isko.Tv.Experimental`: the same browser sources under a separate Lab app ID.
- `src/Isko.Tv.Legacy`: a separate API11/Tizen 8 prototype, not the default package.
- `src/Isko.Diagnostics`: generic read-only platform-probe source, with no recorded data.
- `tools/fixture-server`: generic controlled pages and original media generation.

Optional fixture servers bind loopback by default. See [fixtures](docs/fixtures.md)
for explicit LAN testing. No test server or remote-control client is bundled into
normal TV packages. A CI workflow is provided as
[an inactive template](tools/ci/verify.example.yml); see [verification](docs/testing.md).

Read [architecture](docs/app.md), [browser/video limits](docs/browser-video-analysis.md),
[engine hooks](docs/experimental-engine.md), [API audit](docs/nui-api12.md),
[protection](docs/protection.md) and [third-party notices](THIRD_PARTY_NOTICES.md).
