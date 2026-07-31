# Net.Agora.Mac

[![NuGet](https://img.shields.io/nuget/v/Net.Agora.Video.Mac?label=nuget)](https://www.nuget.org/packages/Net.Agora.Video.Mac)
[![Targets: net8.0 | net9.0 | net10.0](https://img.shields.io/badge/targets-net8.0%20%7C%20net9.0%20%7C%20net10.0%20(macos)-512BD4)](#packages)
[![AgoraRtcEngine_macOS 4.6.2](https://img.shields.io/badge/AgoraRtcEngine__macOS-4.6.2-099DFD)](https://docs.agora.io/en/sdks?platform=macos)
[![Licence: MIT](https://img.shields.io/badge/licence-MIT-green)](LICENSE)

.NET for macOS (AppKit) bindings for Agora's **native macOS** SDKs.

> **Native macOS, not Mac Catalyst.** Agora ships no `maccatalyst` slice for any of its SDKs — the
> iOS xcframeworks are `ios-arm64` + simulator only. Since Mac Catalyst is the *only* desktop-Mac
> target .NET MAUI has, a MAUI app cannot link Agora on the Mac. These packages target
> `net*-macos` (the Microsoft.macOS / AppKit workload) instead, which is the path that actually
> runs Agora on macOS. There is no MAUI companion here for that reason — use these from a native
> .NET for macOS (AppKit) app, or through the cross-platform
> [`Net.Agora`](https://github.com/sbokatuk/Net.Agora) façade's `net9.0-macos` leg.

Three products are bound, from `net8.0-macos` through `net10.0-macos`:

| Package | Native SDK | Use it when |
| --- | --- | --- |
| `Net.Agora.Video.Mac` | `AgoraRtcEngine_macOS` | The app shows or sends video (also carries the full audio surface). |
| `Net.Agora.Voice.Mac` | `AgoraRtcEngine_macOS` (audio surface) | Audio only — the same engine driven with an audio-only API. |
| `Net.Agora.Signaling.Mac` | `AgoraRtm_Apple` (macOS slice) | Realtime messaging (Signaling / RTM 2.x, its own 2.2.x version line) — coexists with either RTC package **from 2.2.8.3**; earlier versions could stop one of the two loading (see the 2.2.8.3 release note). |

> **No Whiteboard, Chat, IoT or Fastboard.** Agora ships no native macOS SDK for these. netless's
> Interactive Whiteboard is a UIKit source pod with no AppKit build (its "macOS" offering is the
> JavaScript web SDK for Electron/web apps), and there is no macOS Chat, IoT or Fastboard SDK — so
> none of them has a `.Mac` binding, matching what Agora actually distributes for the desktop.

Alongside them, twelve `Net.Agora.Extensions.<Name>.Mac` packages carry the RTC SDK's optional
features — AI noise suppression, virtual background, spatial audio, the video enhancement filters,
the software encoders and the rest — mirroring the extension xcframeworks inside the
`AgoraRtcEngine_macOS` archive. They are native payload only: the switch that turns each one on
already exists on `AgoraRtcEngineKit`, and what these packages add is the framework the engine
loads when it is flipped. Add one alongside either RTC package — they depend on neither, so they do
not force a flavour. See [src/Agora.Extension.md](src/Agora.Extension.md) for the full list.

```bash
dotnet add package Net.Agora.Video.Mac   # or Net.Agora.Voice.Mac
```

Pick **one of the RTC pair**: both bind a framework named `AgoraRtcKit`, so referencing both
packages collides at bundle time. The bindings' namespaces follow the package
(`Net.Agora.Video.Mac` / `Net.Agora.Voice.Mac`).

These are raw platform bindings — hand-written surfaces over `AgoraRtcEngineKit`, scoped to what
the cross-platform clients need: engine lifecycle, join/leave, publish/mute, audio routing and
device selection, volume indication, token renewal and connection state, plus the camera surface in
Video (rendering into an `NSView`). Most apps want the cross-platform clients instead:
[`Net.Agora.Video` / `Net.Agora.Voice`](https://github.com/sbokatuk/Net.Agora), which wrap these
packages and their Android/iOS siblings behind one API.

```csharp
using Net.Agora.Video.Mac;

var config = new AgoraRtcEngineConfig { AppId = "<APP_ID>" };
var engine = AgoraRtcEngineKit.SharedEngine(config, del: null);

engine.EnableVideo();
engine.JoinChannel(token: null, channelId: "my-channel", info: null, uid: 0, joinSuccess: null);
```

---

## How this repository works

This repository is the *only* thing that binds Agora's macOS SDKs:
[`Net.Agora`](https://github.com/sbokatuk/Net.Agora) (the cross-platform façade),
[`Net.Agora.iOS`](https://github.com/sbokatuk/Net.Agora.iOS) and
[`Net.Agora.Android`](https://github.com/sbokatuk/Net.Agora.Android) are separate repositories, each
with their own release cadence. Each package's version is `<native version>.<binding revision>` —
see `Directory.Build.props`. The package set lives in `build/packages.tsv`; adding a package means
adding a row there, a project under `src/`, and a fetch script under `build/`.

### What is bound, and why by hand

Objective Sharpie's bundled clang cannot parse any Xcode SDK from 15.3 onward, so
`src/Net.Agora.Video.Mac/ApiDefinition.cs` and `StructsAndEnums.cs` are hand-written straight from
the real headers (`AgoraRtcEngineKit.h`, `AgoraObjects.h`, `AgoraEnumerates.h`), scoped to what the
cross-platform client needs. The macOS `AgoraRtcEngineKit` surface is materially the same
Objective-C API as iOS, with two differences that matter here: the video canvas renders into an
**`NSView`** rather than a `UIView`, and audio/video **device selection** (enumerate and choose a
microphone, speaker or camera) is a macOS-only surface.

### One native slice, no simulator

Every macOS xcframework ships a single universal `macos-arm64_x86_64` slice — there is no simulator
on macOS. That simplifies two things versus the iOS repository: the fetch scripts stage one slice,
and the smoke tests run the built AppKit app **directly on the host Mac** rather than on a
simulator.

## Building locally

```sh
./build/fetch-video.sh            # downloads + SHA-256-verifies the xcframeworks into src/Net.Agora.Video.Mac/lib
./build/fetch-extensions.sh       # the twelve extension frameworks, from the same archive
./build/fetch-voice.sh            # Net.Agora.Voice.Mac (same archive, audio surface)
./build/fetch-signaling.sh        # Net.Agora.Signaling.Mac (RTM 2.x, macOS slice)
./build/BuildNugets.sh            # packs into ./artifacts
dotnet test tests/Net.Agora.Mac.PackageTests
```

The fetch scripts refuse to proceed if the downloaded archive's hash doesn't match
`build/checksums.txt` — see that file for how to record a new version's hash.

No single .NET SDK builds net8, net9 *and* net10 for macOS, so `BuildNugets.sh` packs twice (the
installed SDK's band, then a `net10` pass from a scratch `global.json`) and merges the results —
see `build/merge-packages.py`.

## Tests

**Package tests** run anywhere and inspect the packed `.nupkg`s — a binding assembly and a native
payload for every target framework, every expected xcframework present, the payload logically
identical across target frameworks (which is what would catch a mismatched net10 merge graft), a
single `macos-arm64_x86_64` slice, and manifests consistent with the slices shipped.

**Smoke tests** (`tests/Net.Agora.Mac.DeviceTests`) build an AppKit app against the packed package
and drive the raw binding on the host Mac — the only way to prove the native frameworks actually
link and load (the historical example being `video_dec`, which AgoraRtcKit's binary loads at launch
and whose absence nothing at compile or link time can see). The checks assert every framework is
loaded into the process, read the native SDK version, and round-trip the engine create → drive →
destroy with an unregistered App ID; nothing touches the network and no credentials are involved.

```sh
./.github/scripts/run-macos-tests.sh 4.6.2.1 net10.0-macos
./.github/scripts/run-macos-tests.sh 4.6.2.1 net10.0-macos Voice
```

## CI

| Workflow | Trigger | What it does |
| --- | --- | --- |
| [`pr.yml`](.github/workflows/pr.yml) | pull request | Packs every package as `<version>-beta.<pr>.<run>`, runs the package tests, builds the samples, runs the host smoke tests (per package), then publishes the betas to nuget.org. Forked PRs build and test but skip publishing. |
| [`release.yml`](.github/workflows/release.yml) | tag `v*` / `signaling-v*` | Same build and tests at the tag's track and version, publishes to nuget.org, then creates a GitHub release from `docs/release-notes/<tag>.md`. |

Both call the reusable [`build.yml`](.github/workflows/build.yml), which runs on macOS throughout.

## Samples

The AppKit sample apps under `samples/` join a channel and drive each product directly against the
raw binding — proof the packages are consumable end to end from a native macOS app. They consume
the packed packages from `./artifacts` (see `NuGet.config`), so fetch and pack first.

## Licence

MIT — see [LICENSE](LICENSE). Agora's own SDK is distributed under Agora's SDK licence terms.

[agora]: https://www.agora.io/en/
[facade]: https://github.com/sbokatuk/Net.Agora
