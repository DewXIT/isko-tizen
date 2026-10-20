# Separate legacy prototype

src/Isko.Tv.Legacy is an archived API11/Tizen 8 prototype using deprecated
Tizen.WebView and ElmSharp. It has a separate app ID and is not the selected
Tizen 9 browser. Do not bundle removed platform DLLs to make it load on newer TVs.

```sh
dotnet build src/Isko.Tv.Legacy/Isko.Tv.Legacy.csproj -c Release
```

Reference compilation checks signatures only. Deprecation warnings are retained.
No current device compatibility, installation or playback result is asserted.
The default backend is documented in [app.md](app.md).
