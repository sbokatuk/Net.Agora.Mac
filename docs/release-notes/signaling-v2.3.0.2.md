## What's changed

`Net.Agora.Signaling.Mac` advances to **2.3.0.2**. `AgoraRtmKit` stays at 2.3.0 and the managed API
doesn't change. The one difference is the `aosl` it ships: **1.3.16**, from `AgoraRtcEngine_macOS`
4.7.0, instead of 1.3.5 from 4.6.2.

## Why this needs a release

Signaling, Video and Voice all put `aosl` at `Contents/Frameworks/aosl.framework`, so an app bundle
keeps one copy and every product in the app runs against it. `build/fetch-signaling.sh` takes
Signaling's copy out of the RTC archive to make the packages byte-identical, so moving the RTC pin
moves this payload too.

Use the pair together:

| With | Use |
| --- | --- |
| `Net.Agora.Video.Mac` or `Net.Agora.Voice.Mac` 4.7.0.1 | `Net.Agora.Signaling.Mac` 2.3.0.2 |
| `Net.Agora.Video.Mac` or `Net.Agora.Voice.Mac` 4.6.2.x | `Net.Agora.Signaling.Mac` 2.3.0.1 |

## `aosl` against RTM

`AgoraRtm_Apple` 2.3.0 still pairs with `aosl` 1.3.5, so this package now ships a newer `aosl` than
Agora publishes beside RTM. That direction is safe at the symbol level: all 115 `aosl` symbols
`AgoraRtmKit` 2.3.0 imports are exported by 1.3.16, and so are the 145 that `AgoraRtcKit` 4.7.0
imports.

## Verified

- `tests/Net.Agora.Mac.PackageTests` passes, including the `aosl` byte identity check against
  `Net.Agora.Video.Mac`, and the host smoke suite passes on `net8.0-macos15.0`, `net9.0-macos15.0`
  and `net10.0-macos26.0`.
- One process with Signaling and Video on the shared `aosl` 1.3.16: the RTC engine joined a channel
  and published video, and the RTM client started and reached the Agora service.
- Not verified: a completed login, subscribe or publish. The test project doesn't have Signaling
  enabled yet, so the server refuses the login with -10015.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Signaling.Mac` | 2.3.0.2 | `AgoraRtmKit` 2.3.0 + `aosl` 1.3.16 (from `AgoraRtcEngine_macOS` 4.7.0) |

Target frameworks: `net8.0-macos15.0`, `net9.0-macos15.0`, `net10.0-macos26.0`.
