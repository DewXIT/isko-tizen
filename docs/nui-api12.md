# API12 reference audit

Default compile-time reference: Tizen.NET.API12 12.0.0.18510. Device target:
net6.0-tizen9.0. A reference signature does not prove a particular TV supplies
its native implementation.

Isko.Core.Tests inspects compiled assembly metadata without loading the TV runtime.
It rejects legacy WebView/ElmSharp dependencies in the default shell and checks
all hidden API12 references against exact reviewed signatures.

Reviewed surfaces include navigation/new-window policies; JS evaluation; history
Back and fullscreen exit/events; WebContext HTTP interception; image fitting/tint
and desired decode dimensions; and the VideoHoleEnabled getter/setter. Hidden
members are allowed only after review of SDK availability and documented privilege.

The inspector start/stop methods are reviewed for explicit diagnostic builds only.
Normal packaging forces diagnostics off and its artifact audit rejects an inspector
reference. Do not distribute a diagnostic package to untrusted networks.

The audit does not establish native callback order, decoding, hardware video,
request cancellation, IME behavior, endurance or store acceptance. See
[TizenFX API12 source](https://github.com/Samsung/TizenFX/tree/API12) and the pinned
NuGet reference metadata for the declarations used by the test.
