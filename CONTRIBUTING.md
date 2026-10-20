# Contributing to Isko Tizen

Contributions are welcome: code, reproducible bug reports, performance analysis,
translations and documentation. Start with the [roadmap](docs/roadmap.md) and
[open issues](https://github.com/DewXIT/isko-tizen/issues). For a substantial change,
describe the problem and proposed approach in an issue before implementing it.
English and Polish reports are welcome.

## Work locally

1. Fork and clone the repository. Create a branch for one focused change.
2. Read [AGENTS.md](AGENTS.md), [architecture](docs/app.md) and
   [verification notes](docs/testing.md).
3. Make the change using the existing C#/.NET and Node stack. Run the relevant
   checks listed in [README.md](README.md#local-verification).
4. Run `node tools/check-public-data.cjs` and `git diff --check` before committing.
5. Open a pull request explaining the problem, resulting behavior and actual checks.

Portable checks and documentation need no TV. Device installation uses your own
private configuration and signing profile; see [installation](docs/install.md).
The `Verify` workflow runs the portable checks on every push and pull request; it does not run on a TV, so report any device results explicitly.

## Design constraints

- Target Tizen 9 / API12 for the default app. Keep one application-owned WebView.
- Preserve the website's player, browser security rules and origin-only app history.
- Keep page input bounded. Measure performance rather than claiming unmeasured gains.
- New hidden SDK members need a privilege review and an entry in the compiled API
  audit. Do not bypass the SDK with reflection, P/Invoke or copied platform DLLs.
- Keep compilation, DOM doubles, host fixtures and TV execution distinct in reports.
  A passing local check is not proof of TV playback or native callback behavior.

## Protect your data

Never commit local configuration, certificates, signing profiles, generated packages,
SDK files, TV identifiers, pairing data, cookies or tokens. Redact network addresses,
personal paths, page/account text and query strings from logs and screenshots.
Use a public origin or generic fixture to explain a problem. Do not paste raw
diagnostic dumps. Report security issues through [SECURITY.md](SECURITY.md).

## Collaboration and license

Follow the [code of conduct](CODE_OF_CONDUCT.md). Keep reviews specific, constructive
and focused on behavior and evidence. Small patches with clear reproduction steps
are easier to review. Contributions are made under the project's [MIT license](LICENSE);
retain license notices and document any new third-party dependency.
