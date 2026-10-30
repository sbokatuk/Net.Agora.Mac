## What's changed

`Net.Agora.Signaling.Mac` advances to **2.3.0.1**: the native SDK moves from `AgoraRtmKit` 2.2.8
to **2.3.0**, and the binding revision resets to 1 with it. This is the bump the daily upstream
check asked for in [#7](https://github.com/sbokatuk/Net.Agora.Mac/issues/7) — but it is **not a
pin-only bump**. 2.3.0 deletes the connection-state surface this binding carried, and
a binding that keeps exporting a selector the SDK no longer declares compiles fine and simply stops
firing. So that surface is ported, not kept.

## Breaking changes

Removed from the managed API, because RTM 2.3.0 removed them from the native one (2.2.8 had
already marked them deprecated):

| Removed | Was |
| --- | --- |
| `AgoraRtmClientDelegate.ConnectionChangedToState(AgoraRtmClientKit, string, AgoraRtmClientConnectionState, nint)` | `- (void)rtmKit:channel:connectionChangedToState:reason:` |
| `AgoraRtmClientConnectionState` (enum) | `AgoraRtmClientConnectionState` |
| the raw `nint reason` of that callback | `AgoraRtmClientConnectionChangeReason` |

A delegate subclass that overrides `ConnectionChangedToState` **will not compile** against 2.3.0.1.
That is the intended failure: the alternative was a method the SDK never calls.

### What to use instead

`AgoraRtmClientDelegate.DidReceiveLinkStateEvent(AgoraRtmClientKit, AgoraRtmLinkStateEvent)`, bound
over `- (void)rtmKit:didReceiveLinkStateEvent:`, which is the only way 2.3.0 reports the client's
link. It is a different shape rather than a rename — it describes the client's link to the RTM
service, and which channels a transition affected, instead of one channel's connection — so the
port is mechanical but not one-for-one:

```csharp
// 2.2.8.x
public override void ConnectionChangedToState(
    AgoraRtmClientKit rtmKit, string channelName, AgoraRtmClientConnectionState state, nint reason)
    => Log($"{channelName}: {state}");

// 2.3.0.1
public override void DidReceiveLinkStateEvent(AgoraRtmClientKit rtmKit, AgoraRtmLinkStateEvent e)
    => Log($"link {e.PreviousState} -> {e.CurrentState} ({e.Reason}); " +
           $"affected: {string.Join(", ", e.AffectedChannels)}");
```

`AgoraRtmLinkStateEvent` is bound with all ten properties:

- `CurrentState`, `PreviousState` — `AgoraRtmLinkState`, the enum that takes
  `AgoraRtmClientConnectionState`'s place. **Its values deliberately do not line up with the old
  enum**: `Idle = 0`, `Connecting = 1`, `Connected = 2`, `Disconnected = 3`, `Suspended = 4`,
  `Failed = 5`. There is no `Reconnecting` — a transient drop shows as `Suspended`, and
  `Disconnected` means the link is down until the client acts. Code that compared against numeric
  values, or switched on `Reconnecting`, needs rewriting rather than renaming.
- `ServiceType` (an `NS_OPTIONS` bitmask: None / Message / Stream), `Operation`
  (`AgoraRtmLinkOperation`, 10 values: Login, Logout, Join, Leave, ServerReject, AutoReconnect,
  Reconnected, HeartbeatTimeout, ServerTimeout, NetworkChange) and `ReasonCode`
  (`AgoraRtmLinkStateChangeReason`, 38 values) — carried raw as `nint`, the way
  `AgoraRtmErrorInfo.ErrorCode` already is. A caller that cares reads `Reason`, the SDK's own text
  for the transition.
- `Reason` (`string`, nullable), `AffectedChannels` and `UnrestoredChannels` (`string[]`),
  `IsResumed` (`bool`), `Timestamp` (`ulong`).

Nothing else in the bound surface moved. The rest of the 2.2.8 → 2.3.0 header diff is new error
codes, a `totalOccupancy` field on presence intervals and a `parameters` string on the client
config, none of them bound here; the awaitable operations from 2.2.8.2 and the `aosl` handling from
2.2.8.3 are unchanged.

## What 2.3.0 brings natively

From Agora's
[Signaling release notes for macOS](https://docs.agora.io/en/signaling/overview/release-notes?platform=macos)
(2.3.0, released 2026-08-06):

- **Compatibility change** — the connection-state-changed callback is removed; the link-state event
  is the replacement. That is the removal ported above.
- Presence can be enabled or disabled per project in Agora Console (enabled by default), with four
  new error codes to go with it: `PRESENCE_INACTIVE`, `PRESENCE_PENDING_REQUEST_CANCELLED`,
  `PRESENCE_PENDING_REQUEST_EXCEED_LIMITATION` and `CHANNEL_SUBSCRIBE_CANCELED`. Presence queries
  made before the service is ready after login now retry automatically.
- Large-channel optimisation for channels with more than 512 online users, and faster channel
  subscription; subscribe requests issued while disconnected are re-sent after reconnection.
- Fixes: connection-state notifications were wrong when the app went to the background before
  login; reconnection after a network switch is faster.

These arrive with the native framework and need nothing from the managed side.

## `aosl`

Still staged out of the RTC archive, as since 2.2.8.3 — and the comments explaining why are now
true of 2.3.0 rather than of 2.2.8. `AgoraRtm_Apple` pairs 2.3.0 with `aosl` **1.3.5**, the same
release `AgoraRtcEngine_macOS` 4.6.2 carries, so the version conflict that forced the switch is
gone. The reason is not: "same version" is not "same bytes" across two separately published
builds, and byte identity is what `tests/Net.Agora.Mac.PackageTests` asserts. Re-checked for this
bump: all 115 `aosl` symbols `AgoraRtmKit` 2.3.0 imports (116 under 2.2.8) are exported by the
`aosl` staged from the RTC archive, and `build/fetch-signaling.sh` still refuses to finish without
`_aosl_data_ptr`.

## Checksum

`build/checksums.txt` replaces its `AgoraRtmKit.xcframework_2.2.8` entry with

```
AgoraRtmKit.xcframework_2.3.0 a789d38bf63c97ab9db6c6574fd17de3e820dae44b449ca680624a57fcebb3b3
```

computed from the download and matching the checksum `AgoraRtm_Apple`'s own `Package.swift`
declares for the same asset.

## Cross-platform consumers

The sibling repositories have **not** moved yet:
[`Net.Agora.Android`](https://github.com/sbokatuk/Net.Agora.Android) and
[`Net.Agora.iOS`](https://github.com/sbokatuk/Net.Agora.iOS) are both still at Signaling
`2.2.6.3`. An app — or the `Net.Agora` facade — that shares RTM code across macOS, iOS and Android
therefore sees the link-state event on macOS and the connection-state callback elsewhere until
those repositories catch up; keep the connection-state handling behind a platform condition rather
than deleting it.

`Net.Agora.Signaling`'s Apple source set overrides `ConnectionChangedToState` for both platforms, so
its macOS leg needs the same `#if MACOS` split before it can re-pin to this package. A plain version
bump there does not compile.

## Verified

- `build/fetch-signaling.sh` runs clean and stages both frameworks against the new checksum.
- All 115 `aosl` symbols `AgoraRtmKit` 2.3.0 imports are exported by the staged `aosl`, re-checked
  with `nm`.
- Packed on both SDK bands. `tests/Net.Agora.Mac.PackageTests` passes, including the `aosl` byte
  identity check against `Net.Agora.Video.Mac`.
- The host smoke suite passes on `net8.0-macos15.0`, `net9.0-macos15.0` and `net10.0-macos26.0`.
- Not verified: login, subscribe and publish against the Agora service. The test project does not
  have Signaling enabled yet, so the server refuses the login.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Signaling.Mac` | 2.3.0.1 | `AgoraRtmKit` 2.3.0 + `aosl` 1.3.5 (from `AgoraRtcEngine_macOS` 4.6.2) |

Target frameworks: `net8.0-macos15.0`, `net9.0-macos15.0`, `net10.0-macos26.0`.
