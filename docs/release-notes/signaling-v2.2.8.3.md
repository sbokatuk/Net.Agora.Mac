## What's changed

`Net.Agora.Signaling.Mac` advances to **2.2.8.3**. The managed binding is unchanged and
`AgoraRtmKit` is still 2.2.8; what moves is which `aosl` the package carries.

### Signaling no longer risks breaking Video and Voice in the same app

Both this package and the RTC packages ship an `aosl.xcframework` — Agora's own infrastructure
library, which `AgoraRtcKit` and `AgoraRtmKit` each link as an `@rpath` dependency. An app that
references both does **not** get one each: they unpack to the same
`Contents/Frameworks/aosl.framework`, the bundle carries exactly one, and which one is whichever
the build happened to keep. Both products then run against it.

Up to 2.2.8.2 that was a real hazard. `AgoraRtm_Apple`'s `Package.swift` pairs RTM 2.2.8 with
`aosl` **1.3.0**, while `AgoraRtcEngine_macOS` 4.6.2 carries **1.3.5**, and 1.3.0 does not export
`_aosl_data_ptr` — one of the 144 `aosl` symbols `AgoraRtcKit` resolves at load. An app referencing
Signaling and Video or Voice could therefore fail to bring up the RTC engine, decided by nothing
the developer controls and reported by nothing in the build.

`build/fetch-signaling.sh` now stages `aosl` out of the RTC archive rather than from the
`aosl.xcframework_1.3.0.zip` that sits beside `AgoraRtmKit` on the same server. Agora publishes no
standalone `aosl` 1.3.x for macOS — 1.3.0 is the only one on that path — so the RTC archive is the
only place to get it, and taking it from there makes the two copies literally the same file.

**If you use Signaling together with Video or Voice, upgrade.** Signaling on its own was never
affected, and nothing regressed for it: all 116 `aosl` symbols `AgoraRtmKit` imports are exported
by 1.3.5 as well.

The sibling repositories shipped the same class of bug and are fixed in the same round —
[`Net.Agora.Android`](https://github.com/sbokatuk/Net.Agora.Android) `signaling-v2.2.6.3`, where
`agora-rtm` vendors its own stale `libaosl.so` and `RtcEngine.Create()` returned `null` on a real
device, and [`Net.Agora.iOS`](https://github.com/sbokatuk/Net.Agora.iOS) `signaling-v2.2.6.3`.

### The conflict cannot come back quietly

Being invisible in a build is what made this dangerous, so both guards fail rather than warn:

- `build/fetch-signaling.sh` refuses to finish if the staged `aosl.framework` does not export
  `_aosl_data_ptr` — the symptom, not a version string.
- `tests/Net.Agora.Mac.PackageTests` asserts the `aosl` this package packs is byte for byte the one
  `Net.Agora.Video.Mac` packs. A plist version can agree while the binary does not, so the check is
  on bytes.

`build/checksums.txt` drops its `aosl.xcframework_1.3.0` entry, which nothing fetches any more;
`fetch-signaling.sh` verifies the RTC archive against the same entry `fetch-video.sh` uses, so the
two scripts cannot end up staging `aosl` from differently-hashed downloads.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Signaling.Mac` | 2.2.8.3 | `AgoraRtmKit` 2.2.8 + `aosl` 1.3.5 (from `AgoraRtcEngine_macOS` 4.6.2) |

Target frameworks: `net8.0-macos15.0`, `net9.0-macos15.0`, `net10.0-macos26.0`.
