# Security policy

Isko is an early alpha. Only the current `main` source tree is maintained; there
are no stable releases or supported historical release branches yet. Alpha tags
are source snapshots, not security or compatibility certifications.

## Report a vulnerability privately

Use [GitHub private vulnerability reporting](https://github.com/DewXIT/isko-tizen/security/advisories/new).
Include the affected commit, a minimal reproduction, likely impact and a suggested
fix if available. Use a controlled fixture and invented data wherever possible.
Do not disclose exploitable details in a public issue or pull request before review.
There is no guaranteed response time or paid bounty program.

Do not send private keys, certificate passwords, signed TV packages, pairing tokens,
cookies or personal network/device identifiers, even in a private report. Remove
private values from paths, URLs, logs and screenshots first. If a credential was
exposed, revoke or rotate it with its provider; deleting a post does not revoke it.

## Scope and defaults

Relevant concerns include unsafe navigation/request policy, script injection,
remote-input abuse, persistence/telemetry leaks, release packaging and SDK hooks.
Issues in Samsung's platform or a third-party site may need that vendor's own
reporting channel.

Normal packaging disables the runtime inspector and debug symbols. Private signing
configuration stays outside source control; each installer supplies their own.
Developer fixtures bind loopback by default and should only be exposed deliberately
on a trusted test network. See [installation and privacy defaults](docs/install.md).

The public-data gate checks known patterns and file types. It supplements review;
it cannot recognize every secret or prove that arbitrary content is safe to publish.
