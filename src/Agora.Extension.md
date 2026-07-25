# The extension packages

`Net.Agora.Extensions.*.Mac` are not bindings. They carry native payload and nothing else.

## Why they exist at all

Agora's RTC SDK ships its optional features — AI noise suppression, virtual background, spatial
audio, the software encoders and so on — as separate frameworks. On macOS they travel *inside* the
`AgoraRtcEngine_macOS` archive rather than as separate downloads, as CocoaPods subspecs
(`AgoraRtcEngine_macOS/VirtualBackground` and friends), so a pod consumer opts into each one and
gets only what it asked for.

The switch that turns each one on is already on `AgoraRtcEngineKit`, and therefore already in
`Net.Agora.Video.Mac` / `Net.Agora.Voice.Mac`. What is missing without these packages is the
framework the engine loads when the switch is flipped, and the failure mode is a runtime error
code from a call that compiled and linked perfectly.

So each extension is one package, mirroring the subspecs, and an app pays only for the ones it
turns on. That is the whole point of Agora splitting them: `AgoraSpatialAudioExtension` alone is
tens of MB across the one macOS slice.

## What the packages look like

Every one of them is one (or, for `VideoEncoder`, two) `.xcframework` with no Objective-C class a
consumer would call — the engine loads them itself. So each project's `ApiDefinition.cs` is
deliberately empty and the packed assembly is a ~6 KB stub next to the real payload in
`<Assembly>.resources.zip`, which is why `tests/Net.Agora.Mac.PackageTests` holds them to a
different set of expectations than the four real bindings.

They are still *binding* projects, because that is what packs a `NativeReference` as
`<Assembly>.resources.zip` — and the iOS SDK refuses to build one without an API definition file
("No API definition file specified"), which is the only reason those empty files exist.

`build/fetch-extensions.sh` stages them out of the same `AgoraRtcEngine_macOS` archive
`build/fetch-video.sh` already downloads and checksums, so there is nothing new to pin in
`build/checksums.txt`.

They deliberately depend on **neither** RTC package. The Video and Voice bindings are mutually
exclusive in one app, so depending on either would force a flavour on the consumer; and the audio
extensions work with both. The README tells consumers to add one alongside whichever RTC package
they already have.

## What is not here

**Screen capture** has no extension package. `AgoraScreenCaptureExtension.xcframework` is in the
archive, but ordinary screen sharing does not need it: on macOS `AgoraRtcEngineKit`'s
capture-by-display API is in the core SDK and works with `Net.Agora.Video.Mac` alone (there is no
iOS-style Broadcast Upload Extension on the desktop).

**The low-latency variants** (`AgoraAiNoiseSuppressionLLExtension`,
`AgoraAiEchoCancellationLLExtension`) are present in the archive but have no Android artifact at
4.6.3 — the newest on Maven is 4.5.2 — so there is no pair to publish without opening a second
native version line, matching the choice made in the iOS repository.

**LipSync** (`AgoraLipSyncExtension`) is in the same position: a macOS/iOS framework with no
Android artifact under any spelling.

The package set here is deliberately the same twelve the iOS and Android repositories ship, so a
cross-platform app turns the same features on everywhere.
