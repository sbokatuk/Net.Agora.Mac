## What's changed

**Signaling debuts on macOS**: `Net.Agora.Signaling.Mac` **2.2.8.1** — .NET for macOS (AppKit)
bindings over Agora Signaling (RTM 2.x), `AgoraRtmKit 2.2.8`. Log in to Agora Signaling, subscribe
to channels and publish/receive messages, from a native `net*-macos` app. Coexists with either RTC
package.

The macOS frameworks come from the `AgoraRtm_Apple` Swift package's binary xcframeworks
(`download.agora.io/rtm2/release/`), which ship one universal xcframework spanning iOS, macOS and
visionOS; the fetch script keeps only the `macos-arm64_x86_64` slice. `AgoraRtmKit` plus its one
`@rpath` dependency, `aosl` 1.3.0.

Ships `net8.0-macos`, `net9.0-macos` and `net10.0-macos`.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Signaling.Mac` | 2.2.8.1 | `AgoraRtmKit` 2.2.8 + `aosl` 1.3.0 |
