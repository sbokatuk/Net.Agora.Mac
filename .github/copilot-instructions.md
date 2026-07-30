# Copilot instructions for `Net.Agora.Mac`

## Overview

- This repository hand-binds Agora's **native macOS (AppKit)** SDKs for .NET, targeting
  `net8.0-macos15.0`, `net9.0-macos15.0` and `net10.0-macos26.0`.
- **Native macOS, never Mac Catalyst.** Agora ships no `maccatalyst` slice for any of its SDKs, so
  a MAUI app — whose only desktop-Mac head is Catalyst — cannot link Agora. `net*-macos` is the
  path that works, and it is the only one this repository targets.
- Products: `Net.Agora.Video.Mac` (`AgoraRtcEngine_macOS` 4.6.2 → package 4.6.2.2),
  `Net.Agora.Voice.Mac` (the audio-only surface of that same 4.6.2 archive → 4.6.2.2),
  `Net.Agora.Signaling.Mac` (`AgoraRtm_Apple`, macOS slice, 2.2.8 → 2.2.8.2), plus twelve
  payload-only `Net.Agora.Extensions.<Name>.Mac` packages.
- Consumed from AppKit apps directly, or through the `net*-macos` leg of the
  [`Net.Agora`](https://github.com/sbokatuk/Net.Agora) façade. `Net.Agora.iOS` and
  `Net.Agora.Android` are sibling repositories with their own cadence — do not change them here.

## Build and verify (macOS with Xcode; nothing here builds elsewhere)

```sh
./build/fetch-video.sh && ./build/fetch-voice.sh && ./build/fetch-signaling.sh && ./build/fetch-extensions.sh
./build/BuildNugets.sh                              # packs both SDK bands into ./artifacts
dotnet test tests/Net.Agora.Mac.PackageTests        # runs anywhere, reads artifacts/*.nupkg
```

- Fetch first: nothing native is committed. The scripts download, SHA-256-verify and stage the
  xcframeworks into the gitignored `src/*/lib`.
- Install .NET SDK 9 (`global.json` pins 9.0.100) **and** 10.0.100: `BuildNugets.sh` packs the
  `net9` band, then the `net10` band from a scratch `global.json` it writes itself, and merges the
  two with `build/merge-packages.py`.
- Run `./.github/scripts/select-xcode.sh` before building — .NET for macOS demands the exact Xcode
  carrying the macOS 26.0 SDK that `net10.0-macos26.0` is built against. Locally,
  `AGORA_SKIP_XCODE_CHECK=1` relaxes that check for a smoke run.
- Host smoke tests: `./.github/scripts/run-macos-tests.sh 4.6.2.2 net10.0-macos26.0 Video`
  (also `Voice`, `Signaling`; `2.2.8.2` for Signaling).
- Scoped packs: `./build/BuildNugets.sh --track signaling`, `--suffix beta.12.34`, or both.

## Layout

- `src/` — three binding projects (`ApiDefinition.cs` + `StructsAndEnums.cs` each), twelve
  extension projects (deliberately empty `ApiDefinition.cs`, native payload only), the shared
  `Agora.Binding.props`, and `Agora.Extension.md` (the extension list and what is left out).
- `build/` — `fetch-*.sh`, `checksums.txt`, `packages.tsv` (the package set), `tracks.tsv`
  (tag prefix → track), `pins.sh`, `resolve-track.sh`, `merge-packages.py`, `BuildNugets.sh`,
  `upstream.tsv` + `check-upstream.sh`.
- `tests/Net.Agora.Mac.PackageTests`, `tests/Net.Agora.Mac.DeviceTests`,
  `samples/Net.Agora.Sample.Video.Mac`, `docs/release-notes/<tag>.md`, `.github/workflows` and
  `.github/scripts`. `Net.Agora.Mac.sln` holds the fifteen binding projects only; build the tests
  and the sample by path.

## Conventions

- Package version is `<native SDK version>.<binding revision>`, set **only** in
  `Directory.Build.props`. Shell callers read it with `. build/pins.sh` — never re-parse the props
  file anywhere else. Each product's pin moves independently (RTC 4.6.x, RTM 2.2.x).
- Adding a package = a row in `build/packages.tsv`, a project under `src/`, and its framework
  mapping in the relevant `build/fetch-*.sh`. Nothing else keeps a second copy of the list.
- Bindings are written by hand from the real headers (`AgoraRtcEngineKit.h`, `AgoraObjects.h`,
  `AgoraEnumerates.h`) and scoped to what the cross-platform façade needs. Two macOS differences
  from iOS matter: the video canvas is an **`NSView`**, and audio/video **device selection**
  (`enumerateDevices:` / `setDevice:`) is macOS-only.
- Pin the platform version in every target framework (`-macos15.0`, `-macos26.0`); a bare
  `net9.0-macos` floats and breaks the link under CI's selected Xcode. Keep `PackageReference`
  versions exact and bracketed in the tests and the sample.
- `AgoraSdkBand` is `net9` or `net10` only — the `ValidateAgoraSdkBand` target errors on anything
  else.
- Bump a native version and record its new SHA-256 in `build/checksums.txt` in the same change.

## CI and release flow

- `pr.yml` calls the reusable `build.yml` with `verify: true`: pack every package as
  `<version>-beta.<pr>.<run>`, run the package tests, build the sample against the packed package,
  run the host smoke tests (Video/Voice/Signaling × net8/net10) on macOS runners, then publish the
  betas to nuget.org. Fork pull requests build and test but skip publishing.
- Releasing: merge a pull request that adds `docs/release-notes/<tag>.md` → `auto-release.yml` tags
  the merge and dispatches `release.yml` → the guard job refuses a tag whose commit is not an
  ancestor of the default branch → `build/resolve-track.sh` maps the tag to a track (`v*` = rtc:
  Video, Voice, `Extensions.*`; `signaling-v*` = Signaling) → the build runs with `verify: false`,
  because the pull request already verified that exact commit → push and create the GitHub release
  from the note.
- Publishing is nuget.org trusted publishing over OIDC; `NUGET_USER` is the only secret and login
  happens immediately before the push.
- `upstream-drift.yml` compares the pins against `build/upstream.tsv` daily and files one
  fingerprinted issue per group; reproduce locally with `DRIFT_DIR=/tmp/d ./build/check-upstream.sh`.

## Testing

- Package tests assert the packed artefacts: a binding assembly and a native payload per target
  framework, every expected xcframework, the payload logically identical across target frameworks
  (this is what catches a bad `net10` merge graft), the single `macos-arm64_x86_64` slice, and
  consistent manifests. Re-pack, then re-run them, after any packaging change.
- Device tests are an AppKit smoke app driven on the host Mac: every framework loaded into the
  process, the native SDK version read back, and an engine create → drive → destroy round-trip with
  an unregistered App ID. No network, no credentials, one product per run.
- Before opening a pull request: fetch, pack, and run the package tests locally.

## Hard rules

- Never add a Mac Catalyst target framework.
- Never add Whiteboard, Chat, IoT or Fastboard `.Mac` packages — Agora ships no native macOS SDK
  for any of them (netless's whiteboard pod is UIKit-only; the documented "macOS whiteboard" is the
  JavaScript web SDK for Electron). The absence is researched and deliberate.
- Never commit native artefacts. `src/*/lib` is fetch-script output, SHA-256-verified against
  `build/checksums.txt`; a mismatch must stay a hard failure.
- Never regenerate bindings with Objective Sharpie — its clang cannot parse Xcode SDKs 15.3 and
  later. Extend the hand-written `ApiDefinition.cs` / `StructsAndEnums.cs` instead.
- Never reference both RTC packages in one app or test target: `Net.Agora.Video.Mac` and
  `Net.Agora.Voice.Mac` both ship a framework named `AgoraRtcKit` and collide at bundle time.
- Never expect a simulator. End-to-end means execution on the host Mac; keep the
  single-universal-slice assertions in the package tests intact.
- Never set versions or pins outside `Directory.Build.props`.
- Never bypass the release guard (the tag must be an ancestor of the default branch) and never
  publish outside the OIDC trusted-publishing flow.
- Keep the sample out of `Net.Agora.Mac.sln` and consuming packed nupkgs from `artifacts/`.
- Write prose in British spelling ("licence", "behaviour"), matching the README.

## References

- [`Net.Agora`](https://github.com/sbokatuk/Net.Agora) (façade),
  [`Net.Agora.iOS`](https://github.com/sbokatuk/Net.Agora.iOS) — the shared hand-binding style —
  and [`Net.Agora.Android`](https://github.com/sbokatuk/Net.Agora.Android).
- Agora macOS SDK documentation: <https://docs.agora.io/en/sdks?platform=macos>.
- `src/Agora.Extension.md` for the extension packages and what is deliberately excluded;
  `README.md` for the consumer-facing story.

Trust these instructions and search the codebase only when something here is incomplete or wrong.
