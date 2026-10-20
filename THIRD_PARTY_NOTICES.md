# Third-party provenance and notices

Reviewed 2026-10-19. Isko's application, fixture and protection code is original. No Overscan code or external filter list is included.

| Component | Role | License / evidence |
| --- | --- | --- |
| Tizen.NET.API12 12.0.0.18510 | Default compile-time reference; the TV supplies implementations | Apache-2.0. [Publisher package](https://www.nuget.org/packages/Tizen.NET.API12/12.0.0.18510). Copyright Samsung Electronics Co., Ltd. |
| Tizen.NET.API11 11.0.0.18033 | Compile-time reference assemblies; TV supplies implementations | Apache-2.0. [Publisher's package](https://www.nuget.org/packages/Tizen.NET.API11/11.0.0.18033) links Apache 2.0 and declares no package dependencies. Copyright Samsung Electronics Co., Ltd. |
| ElmSharp | Platform UI API referenced through the Tizen API reference bundle; not an additional vendored runtime | Apache-2.0 in the [API11 source header](https://github.com/Samsung/TizenFX/blob/API11/src/ElmSharp/ElmSharp/Window.cs). Copyright (c) 2018 Samsung Electronics Co., Ltd. |
| Tizen.WebView | Platform WebView API | Apache-2.0 in [API11 source](https://github.com/Samsung/TizenFX/blob/API11/src/Tizen.WebView/Tizen.WebView/WebView.cs). No framework source or DLL is vendored by Isko. |

The upstream license is available in [Samsung/TizenFX LICENSE](https://github.com/Samsung/TizenFX/blob/API11/LICENSE) and [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0). If redistributing covered components later, include the license text, applicable copyright/NOTICE attribution and notices of modifications. This inventory does not authorize bundling platform DLLs: Samsung's [.NET TV FAQ](https://developer.samsung.com/smarttv/develop/faq/tizen-net-tv.html) explicitly says framework libraries must be excluded from the application package.

The selected direct `Tizen.NET.API11` reference has no NuGet transitives according to its publisher. This differs from the alternative [Tizen.NET 11.0.0.18033 metapackage](https://www.nuget.org/packages/Tizen.NET/11.0.0.18033), which declares Microsoft.NETCore.Platforms >= 6.0.9 and Tizen.NET.API11 for net6.0. The metapackage is not required merely to reference API11. Installed Tizen workloads/SDK packs have their own licenses; inspect resolved build assets and package contents before distributing a TPK. No restore or complete SDK dependency inventory was possible in the initial environment.

Overscan's architecture documentation was read for comparison only. No code, assets, bundled rules or license-dependent derivatives were imported. Test rules and media generation instructions are original; no EasyList, uBlock, YouTube-specific list or external film is bundled. If locally generating media with ffmpeg, the tool and selected codec licensing remain properties of that external build tool; ffmpeg is not bundled into Isko.
