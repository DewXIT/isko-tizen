# Build and install using your own TV configuration

The public distribution is source code. Each installer supplies a Samsung TV
certificate profile authorized for their own TV and builds/signs locally. A
device-signed TPK is private output, not a universal package for other TVs.
No device address, pairing token, certificate or SDK account is provided.

## Requirements

- Node 16+; .NET SDK 8 with the Samsung Tizen workload supporting
  `net6.0-tizen9.0`; Samsung Tizen Core CLI (`tz`) and `sdb`.
- A Tizen 9 TV, with development mode configured for **your** computer.
- Your Samsung author/distributor certificate profile, created using Samsung's
  certificate tools for your TV. Keep passwords/keys in that tool's private
  profile store, outside the checkout. No emulator is required.

See [Samsung TV certificates](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/creating-certificates.html)
and [Tizen .NET TV](https://developer.samsung.com/smarttv/develop/tizen-net-tv.html).
Toolchain support and successful packaging are distinct from native playback.

## Private configuration

```sh
mkdir -p .local
cp tools/tv/config.example.json .local/isko-tv.json
```

Edit the copy locally. Set `signingProfile` to your profile's name; set `dotnet`,
`tz`, `sdb` to command names on PATH or local executable paths. If required,
`sdkPath` selects your SDK through `TIZEN_SDK_PATH`. `deviceAddress` is your
TV hostname/IP, optionally followed by the sdb port. Leave it empty for packaging.
No password/key fields are supported. `.local/`, certificates, packages and
`artifacts/` are ignored by Git. The config stays outside the app's project.

```sh
# Validates a build/sign plan; executes nothing and hides local config values:
node tools/tv/package.cjs --dry-run
# Builds and signs; does not contact a TV:
node tools/tv/package.cjs
# Only this explicit option connects, installs and launches on your configured TV:
node tools/tv/package.cjs --install
```

Output stays under `artifacts/`. The build disables the runtime inspector and
debug symbols, maps source paths to a neutral path, and audits the TPK before
installation. Existing packages are updated through `sdb install`; the helper
does not invoke `tz install`'s uninstall-first workflow. Back up anything you
need before using developer tooling; data retention is not a verified guarantee.

The package contains app code, resources and required package signatures, not
the local config/private keys. Certificate signatures can contain identifying
public certificate metadata; keep your signed TPK private. Publish the source
and empty example configuration rather than your local artifacts.

## Network and privacy defaults

The normal app does not start a remote control server, inspector, fixture server
or tunnel. It connects to sites opened by its user; saved-site icons are fetched
separately without cookies. History/favorites store origins only in the TV app's
private data. Browser cookies/site storage are managed by the platform; origin-only
history does not mean websites cannot store their own state.

Developer fixtures bind loopback by default. For a deliberate test on **your own
trusted LAN**, opt in with `ISKO_FIXTURE_HOST=0.0.0.0` and open
`http://YOUR_COMPUTER_LAN_IP:8765/` on your TV. Fixture endpoints are unauthenticated;
stop the development server afterwards. They are not bundled into the TV app.

`IskoRuntimeDiagnostics=true` is an explicit developer build option that starts
a WebView inspector and can expose page content to the local network. It is off
by default and forced off by the packaging helper. Do not distribute that build.
There is no bundled pairing client, device token or configured address in the source.

## Publication checks

```sh
node tools/check-public-data.cjs
node tools/check-public-data.cjs --archive artifacts/tv/PACKAGE.tpk
node tests/packaging.test.cjs
```

The deterministic gate detects common key/token formats, private IPv4 literals,
private workstation paths, certificates, debug symbols and generated files in the
inspected scope. ZIP inspection also checks UTF-16 strings in assemblies. It is
a bounded publication check, not proof that every possible secret is recognizable.
Local config is intentionally ignored and never included in the public source scan.
