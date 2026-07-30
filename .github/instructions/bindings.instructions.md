---
applyTo: "src/**/ApiDefinition.cs,src/**/StructsAndEnums.cs"
---

# Hand-written binding rules

- These files are written by hand from Agora's real macOS headers — `AgoraRtcEngineKit.h`,
  `AgoraObjects.h` and `AgoraEnumerates.h` for RTC, `AgoraRtmClientKit.h` and `AgoraRtmObjects.h`
  for Signaling — at the version pinned in `Directory.Build.props`. Objective Sharpie cannot parse
  Xcode SDKs 15.3 and later, so never regenerate them.
- Mirror the header exactly: selector strings, argument order, nullability and types. Do not invent
  a friendlier Objective-C surface; shape the API in the cross-platform façade instead.
- Keep the two macOS-only differences from the iOS bindings explicit, and comment them where they
  appear: the video canvas renders into an **`NSView`** (not `UIView`), and audio/video **device
  selection** — enumerate and choose a microphone, speaker or camera via
  `enumerateDevices:` / `setDevice:` — exists only on macOS.
- Scope additions to what [`Net.Agora`](https://github.com/sbokatuk/Net.Agora) actually needs:
  engine lifecycle, join/leave, publish/mute, audio routing and device selection, volume
  indication, token renewal, connection state, and the Video camera surface. Do not bind the whole
  SDK speculatively.
- Keep `Net.Agora.Voice.Mac` audio-only: it binds the audio surface of the same engine, so no video
  entry points, and the device tests compile per flavour against that difference.
- Extension projects (`src/Net.Agora.Extensions.*.Mac`) keep an empty `ApiDefinition.cs` — they
  carry native payload only, and the file exists solely because a binding project will not build
  without one. Do not add managed API or a dependency on either RTC package there.
- Every enum and struct a bound member exposes belongs in `StructsAndEnums.cs` with its native
  values; never renumber or reorder members to tidy them.
- A new type or member means new native symbols: check the framework already staged by the matching
  `build/fetch-*.sh` carries it, then re-pack and re-run
  `dotnet test tests/Net.Agora.Mac.PackageTests` plus the host smoke test for that product.
