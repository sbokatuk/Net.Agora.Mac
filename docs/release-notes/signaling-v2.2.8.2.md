## What's changed

`Net.Agora.Signaling.Mac` advances to **2.2.8.2**. The native SDK is unchanged at AgoraRtmKit
2.2.8 + aosl 1.3.0.

### Awaitable operations

All six RTM operations now also have awaitable forms, matching the iOS binding exactly:

```csharp
var result = await client.LoginAsync(token);
if (result.ErrorInfo is { ErrorCode: not 0 } error)
    return Fail(error.Reason);
```

`LoginAsync`, `LogoutAsync`, `RenewTokenAsync`, `SubscribeAsync`, `UnsubscribeAsync`, `PublishAsync`,
each returning `Task<AgoraRtmOperationResult>`.

The task **completes rather than faults** on an RTM error, deliberately: the SDK answers some
successes with a non-nil `errorInfo` whose `errorCode` is 0, so "error object present" is not
"failed". Check `result.ErrorInfo?.ErrorCode`.

`addDelegate:`/`removeDelegate:` are now bound, so a listener can be attached without owning client
construction.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Signaling.Mac` | 2.2.8.2 | AgoraRtmKit.xcframework 2.2.8 + aosl.xcframework 1.3.0, macOS slices only |

Target frameworks: `net8.0-macos15.0`, `net9.0-macos15.0`, `net10.0-macos26.0`.
