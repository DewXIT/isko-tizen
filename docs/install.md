# Build and install using your own TV configuration

The public distribution is source code. Each installer supplies a Samsung TV
certificate profile authorized for their own TV and builds/signs locally. A
device-signed TPK is private output, not a universal package for other TVs.
No device address, pairing token, certificate or SDK account is provided.

Polish version: [install.pl.md](install.pl.md).

## The short path

1. **Get the tools** (once): Node 16+, the .NET 8 SDK, the Samsung Tizen workload, and Samsung's Tizen SDK
   command-line tools (`sdb`, `tz`). [Step 1](#1-tools).
2. **Create a Samsung TV certificate profile** (once, needs your Samsung account and the TV's DUID).
   [Step 2](#2-certificate-profile).
3. **Prepare the TV**: developer mode on, with this computer's IP. [Step 3](#3-the-tv).
4. **Write your private configuration** `.local/isko-tv.json`. [Step 4](#4-private-configuration).
5. **Check everything**: `node tools/tv/doctor.cjs` reports what is missing and how to fix it.
6. **Build, sign, install**: `node tools/tv/package.cjs --install`. [Step 5](#5-build-and-install).

Keep the tools and your certificate profile in **permanent** folders. A temporary folder (`/tmp`) is cleared by the
system, and a lost author certificate cannot be recovered from this repository.

## 1. Tools

- **Node.js 16+** and **.NET SDK 8** ([download](https://dotnet.microsoft.com/download/dotnet/8.0)).
- **Samsung's Tizen .NET workload** (supports `net6.0-tizen9.0`). Samsung's installer script is described in the
  [workload wiki](https://github.com/Samsung/Tizen.NET/wiki/Installing-Tizen-.NET-Workload); it is a script you pipe
  into a shell, so download and read it before you run it. Check with `dotnet workload list` (it must list `tizen`).
- **Tizen SDK command-line tools** from Samsung: `sdb` (device bridge) and `tz` (packaging and signing). They are
  not separate downloads; they ship in the SDK's command-line installer:

  ```sh
  node tools/tv/setup-tools.cjs            # downloads the official installer (size-checked), installs nothing
  ```

  It prints the next commands. Read the license with `--show-license`; installing with `--accept-license <folder>`
  means **you** accept it. Install into a permanent folder such as `~/tizen-sdk`. `sdb` ends up in `tools/`, `tz` in
  `tools/tizen-core/`. The installer comes from `https://download.tizen.org/sdk/Installer/`.
- To add Samsung's **TV extension** or create certificates graphically you need the SDK's package manager
  (`package-manager/package-manager-cli.bin`, from the same install) or the full
  [Tizen Studio](https://developer.tizen.org/) IDE.

## 2. Certificate profile

A Samsung TV profile has an **author certificate** (identifies you; tied to your Samsung account) and a
**distributor certificate** (authorizes your TV by its DUID). Samsung's tools create them; the command-line `tz cert`
makes a generic Tizen certificate, **not** a Samsung one, so it is not enough for a TV.

1. On the TV: **Menu > Support > Contact Samsung > Unique Device ID** shows the DUID. Write it down.
2. In **Tizen Studio** (Tools > Certificate Manager) or the **Samsung TV extension for VS Code**: add a profile,
   choose **Samsung**, **TV**, give the profile a name, create a **new author certificate** (you sign in with your
   Samsung account here and choose a **backup path**), then create a **distributor certificate** with the DUID.
   Samsung's guides: [Tizen Studio](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/creating-certificates.html)
   and [VS Code](https://developer.samsung.com/smarttv/develop/tools/additional-tools/vscode/creating-certificates.html).
3. **Back up `author.p12` and its password** somewhere safe and permanent (Samsung says updates must be signed with
   the same author certificate). Never put certificates, passwords or the profile folder in this repository.
4. Make the profile visible to `tz`: `tz security-profiles list` should show it. If you created it in Tizen Studio,
   its profiles file is the one `tz` reads; otherwise add it with `tz security-profiles add` (see
   [Tizen-Core CLI commands](https://docs.tizen.org/application/tizen-studio/tizen-core/tizen-core-cli/)).

## 3. The TV

- A **Tizen 9** TV. Open **Apps**, type **12345** on the remote, switch **Developer mode ON** and enter the **IP of
  the computer** you build on. Restart the TV when it asks. (After a power cycle, check the IP is still there.)
- The computer and the TV must be on the same network. `sdb connect <tv-ip>:26101` then `sdb devices` should list it.

## 4. Private configuration

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

Example (use your own absolute paths and names; `192.0.2.50` is a documentation address, put your TV's):

```json
{
  "dotnet": "dotnet",
  "tz": "/path/to/tizen-sdk/tools/tizen-core/tz",
  "sdb": "/path/to/tizen-sdk/tools/sdb",
  "signingProfile": "MyTvProfile",
  "deviceAddress": "192.0.2.50"
}
```

Then check the whole setup (changes nothing, never prints the address or reads keys):

```sh
node tools/tv/doctor.cjs
```

## 5. Build and install

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

## .NET version

The app targets `net6.0-tizen9.0`. Microsoft has ended support for .NET 6, so builds show a warning that the project
silences on purpose: the runtime that matters is the one on the TV. A `net8.0-tizen9.0` build compiles with the
current Samsung workload (`--target-framework net8.0-tizen9.0`), but on the tested Tizen 9 TV that package installed
and then did not run: the process restarted at once and nothing connected to the test server. Do not move the
device target to .NET 8 until a TV demonstrably runs it; keep `net6.0-tizen9.0`.

## Troubleshooting

| Symptom | Cause and fix |
| --- | --- |
| `doctor` says a tool is "inside a temporary folder" or `tz`/`sdb` "not found" | Tools installed under `/tmp` were cleared. Reinstall into a permanent folder and update `.local/isko-tv.json`. |
| `Signing profile exists: not listed` | The profile store was removed or moved. Recreate the profile (step 2) or import your backed-up `author.p12`; Samsung certificates cannot be regenerated by this repository. |
| `sdb connect` fails after the TV was restarted | The old connection is stale: `sdb disconnect`, `sdb kill-server`, `sdb start-server`, then `sdb connect <ip>:26101`. If the port is open but it still fails, open Apps > 12345 and check **Developer mode** is ON with your computer's IP; restart the TV after changing it. |
| Install error about the certificate or privileges | The distributor certificate must list your TV's DUID and a privilege level that covers the manifest (`internet`). Recreate it with the right DUID. |
| The app starts and the process restarts at once (a different pid when you run it again) | It crashed. Check the TV firmware is Tizen 9 and the package is the `net6.0-tizen9.0` build; `net8.0-tizen9.0` installs but does not run on the tested TV. |
| `Installation stopped` by the publication audit | The package contains something the public-data gate rejects (a LAN address, a path). Developer builds that embed a start address (`--start-url`) are allowed past the LAN-address rule only. |
| Video stays black while sound plays | The WebView cannot decode H.264 on the tested TV; Isko hands streams to the native player ([native playback](native-playback.md)). A stream that needs a `Referer` header cannot be played yet. |

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
