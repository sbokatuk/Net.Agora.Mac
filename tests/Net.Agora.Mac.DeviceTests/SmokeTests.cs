using System.Runtime.InteropServices;
#if AGORA_VOICE
using Net.Agora.Voice.Mac;
#else
using Net.Agora.Video.Mac;
using AppKit;
#endif
using ObjCRuntime;

namespace Net.Agora.Mac.DeviceTests;

/// <summary>A single on-host check. Throws to fail.</summary>
/// <param name="Name">Human readable name, reported to stdout.</param>
/// <param name="Execute">Runs the check.</param>
public sealed record SmokeTest(string Name, Action Execute);

/// <summary>
/// End-to-end checks that only mean anything when the native frameworks are actually loaded: they
/// load the Agora frameworks out of the packaged xcframeworks and drive the raw binding —
/// <see cref="AgoraRtcEngineKit"/> itself, with no cross-platform façade in between. On macOS there
/// is no simulator, so these run directly on the host that CI (or a developer) is on.
/// </summary>
/// <remarks>
/// Nothing here touches the network. The App ID is syntactically valid but unregistered, and no
/// channel is ever joined. What this suite proves is that the packed package's native payload
/// loads, its selectors resolve, and the engine round-trips create → drive → destroy.
/// </remarks>
public static class SmokeTests
{
    // 32 lowercase hex characters — the shape of a real Agora App ID. Unregistered on purpose:
    // nothing here needs credentials, and there is nothing to leak if CI logs are public.
    private const string AppId = "0123456789abcdef0123456789abcdef";

    /// <summary>Writes a line to stdout under the current check. Set by the app delegate.</summary>
    public static Action<string> Reporter { get; set; } = _ => { };

    /// <summary>The engine every check after construction shares. Set by <see cref="ConstructsTheEngine"/>.</summary>
    private static AgoraRtcEngineKit? _engine;

    /// <summary>
    /// Held for the engine's lifetime: the binding hands the delegate to native code, which does
    /// not keep the managed wrapper alive — a collected delegate would turn a later callback into
    /// a crash rather than a failing check.
    /// </summary>
    private static EngineDelegate? _delegate;

    /// <summary>Every check, in the order they must run. Per flavour — see the csproj.</summary>
    public static SmokeTest[] All =>
    [
        new("every native framework is linked and loadable", EveryFrameworkIsLinked),
        new("reports the native SDK version", ReportsTheSdkVersion),
        new("constructs the engine with an unregistered App ID", ConstructsTheEngine),
#if AGORA_VOICE
        new("enables and disables audio", EnablesAndDisablesMedia),
        new("mutes and unmutes the local audio stream", MutesAndUnmutesLocalStreams),
#else
        new("enables and disables video and audio", EnablesAndDisablesMedia),
        new("mutes and unmutes the local streams", MutesAndUnmutesLocalStreams),
        new("attaches and detaches a local video canvas", AttachesALocalVideoCanvas),
#endif
        new("drives volume indication", DrivesVolumeIndication),
        new("renews a token without a join is reported, not thrown", RenewTokenIsReported),
        new("sets the channel profile and client role", SetsProfileAndRole),
        new("leave without a join is reported, not thrown", LeaveWithoutAJoinIsReported),
        new("destroys the shared engine", DestroysTheEngine),
    ];

    private static void Report(string message) => Reporter(message);

    /// <summary>Every framework the package ships. All dynamic — see the csproj.</summary>
    /// <remarks>
    /// video_dec earns its place twice over: AgoraRtcKit's binary links
    /// <c>@rpath/video_dec.framework/video_dec</c> directly, so a package that dropped it kills the
    /// process at launch — dyld aborts before a single managed frame runs — and nothing at compile
    /// or link time in a consuming app can see it coming. Unlike iOS, both flavours carry it: the
    /// macOS AgoraRtcKit is the full engine even when only its audio surface is bound.
    /// </remarks>
    private static readonly string[] Frameworks =
    [
        "AgoraRtcKit", "aosl", "Agorafdkaac", "Agoraffmpeg", "AgoraSoundTouch", "video_dec",
    ];

    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern uint _dyld_image_count();

    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern IntPtr _dyld_get_image_name(uint index);

    /// <summary>
    /// Proves each of the six xcframeworks actually made it into the app and was loaded.
    /// </summary>
    /// <remarks>
    /// This is the check that catches a packaging regression the compiler cannot see. A binding
    /// assembly reaches its native framework only through selector strings, so a package whose
    /// .resources.zip was empty, or whose xcframework manifest advertised a slice that had been
    /// stripped, still compiles and links — and then fails at runtime the first time a type is
    /// touched. The codec frameworks bind no managed types at all, so for those there is no C#
    /// type whose use would reveal the problem.
    /// </remarks>
    private static void EveryFrameworkIsLinked()
    {
        var images = new List<string>();
        var count = _dyld_image_count();
        for (uint i = 0; i < count; i++)
        {
            var name = Marshal.PtrToStringUTF8(_dyld_get_image_name(i));
            if (name is not null)
            {
                images.Add(name);
            }
        }

        var missing = Frameworks
            .Where(framework => !images.Any(image =>
                image.EndsWith($"/{framework}.framework/{framework}", StringComparison.Ordinal) ||
                image.EndsWith($"/{framework}.framework/Versions/A/{framework}", StringComparison.Ordinal)))
            .ToList();

        Assert(
            missing.Count == 0,
            $"these frameworks were not loaded into the process: {string.Join(", ", missing)}");

        Report($"all {Frameworks.Length} frameworks loaded");
    }

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr object_getClass(IntPtr obj);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool class_respondsToSelector(IntPtr cls, IntPtr sel);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    /// <summary>Reads <c>+[AgoraRtcEngineKit getSdkVersion]</c> and checks its shape.</summary>
    /// <remarks>
    /// Called through the Objective-C runtime rather than the binding, because the binding is
    /// deliberately scoped to what the cross-platform client needs and does not export this
    /// selector. Sending the selector by hand proves the same thing the binding's own calls rely
    /// on, that the class is registered and its selectors resolve against the loaded framework.
    /// </remarks>
    private static void ReportsTheSdkVersion()
    {
        var cls = Class.GetHandle("AgoraRtcEngineKit");
        Assert(cls != IntPtr.Zero, "AgoraRtcEngineKit did not resolve to a native class.");

        var selector = Selector.GetHandle("getSdkVersion");

        Assert(
            class_respondsToSelector(object_getClass(cls), selector),
            "AgoraRtcEngineKit does not respond to +getSdkVersion.");

        var version = Runtime.GetNSObject<Foundation.NSString>(IntPtr_objc_msgSend(cls, selector))?.ToString();

        Report($"native SDK version {version ?? "(null)"}");

        // The shape, not a pinned value: the version lives in Directory.Build.props and asserting it
        // here would mean two places to edit on every bump.
        Assert(!string.IsNullOrWhiteSpace(version), "getSdkVersion returned nothing.");
        Assert(
            char.IsAsciiDigit(version![0]) && version.Contains('.', StringComparison.Ordinal),
            $"'{version}' does not look like a dotted version number.");
    }

    private static void ConstructsTheEngine()
    {
        var config = new AgoraRtcEngineConfig
        {
            AppId = AppId,
            ChannelProfile = AgoraChannelProfile.LiveBroadcasting,
        };

        // The delegate goes in at construction: passing a subclass of the [Model] type is what
        // proves the protocol's exported selectors registered, even though no callback fires in
        // this suite — none of the bound callbacks report anything without a join.
        _delegate = new EngineDelegate();
        _engine = AgoraRtcEngineKit.SharedEngine(config, _delegate);

        Assert(_engine is not null, "SharedEngine returned null.");
        Assert(_engine!.Handle != NativeHandle.Zero, "SharedEngine returned an object with no native handle.");

        Report("engine constructed (process-wide singleton, shared by every later check)");
    }

    private static AgoraRtcEngineKit Engine =>
        _engine ?? throw new InvalidOperationException("the engine has not been constructed yet.");

    private static void EnablesAndDisablesMedia()
    {
        // None of these needs camera or microphone permission — enabling a module is not
        // capturing — so zero is the only acceptable answer. StartPreview is deliberately not
        // called in the Video flavour: it is the first call that touches the camera, which on a
        // headless CI host would raise a TCC permission prompt nobody is there to answer.
#if !AGORA_VOICE
        AssertZero(Engine.EnableVideo(), "enableVideo");
        AssertZero(Engine.DisableVideo(), "disableVideo");
        AssertZero(Engine.EnableVideo(), "enableVideo (again)");
#endif
        AssertZero(Engine.EnableAudio(), "enableAudio");
        AssertZero(Engine.DisableAudio(), "disableAudio");
        AssertZero(Engine.EnableAudio(), "enableAudio (again)");
    }

    private static void MutesAndUnmutesLocalStreams()
    {
        AssertZero(Engine.MuteLocalAudioStream(true), "muteLocalAudioStream(true)");
        AssertZero(Engine.MuteLocalAudioStream(false), "muteLocalAudioStream(false)");
#if !AGORA_VOICE
        AssertZero(Engine.MuteLocalVideoStream(true), "muteLocalVideoStream(true)");
        AssertZero(Engine.MuteLocalVideoStream(false), "muteLocalVideoStream(false)");
#endif
    }

    private static void DrivesVolumeIndication()
    {
        // Volume indication is local engine state and cross-platform, so it must answer zero. No
        // disabling counterpart is exercised: the header documents interval <= 0 as "disable", but
        // the 4.6.2 engine answers -2 (invalid argument) to it, so the claim is the SDK's to sort
        // out. The iOS speakerphone/audio-route setters are not exercised here: they are not
        // implemented on macOS (see the binding's ApiDefinition.cs) and are not bound.
        AssertZero(Engine.EnableAudioVolumeIndication(200, 3, reportVad: false), "enableAudioVolumeIndication");
    }

    private static void RenewTokenIsReported()
    {
        // No join ever happens in this suite, so the value is the SDK's business — what matters
        // is that the call crosses the bridge and comes back as a code rather than a crash.
        var code = Engine.RenewToken("0123456789abcdef0123456789abcdef");

        Report($"renewToken returned {code}");
    }

    private static void SetsProfileAndRole()
    {
        AssertZero(Engine.SetChannelProfile(AgoraChannelProfile.LiveBroadcasting), "setChannelProfile");
        AssertZero(Engine.SetClientRole(AgoraClientRole.Broadcaster), "setClientRole(broadcaster)");
        AssertZero(Engine.SetClientRole(AgoraClientRole.Audience), "setClientRole(audience)");
    }

#if !AGORA_VOICE
    private static void AttachesALocalVideoCanvas()
    {
        // SetupLocalVideo adds the SDK's renderer as a subview of whatever NSView is passed —
        // rendering nothing until a preview or join starts, so this is safe without camera
        // permission. The null teardown is the documented way to detach.
        var view = new NSView();

        AssertZero(
            Engine.SetupLocalVideo(new AgoraRtcVideoCanvas { Uid = 0, View = view }),
            "setupLocalVideo");
        AssertZero(Engine.SetupLocalVideo(null), "setupLocalVideo(nil)");
    }
#endif

    private static void LeaveWithoutAJoinIsReported()
    {
        // No join ever happens in this suite, so the value is the SDK's business — what matters
        // is that the call crosses the bridge, including its block argument, and comes back as a
        // code rather than a crash or an exception.
        var code = Engine.LeaveChannel(stats => Report($"leave block fired with {stats}"));

        Report($"leaveChannel returned {code}");
    }

    private static void DestroysTheEngine()
    {
        // Every reference is released before Destroy, as the binding's own comment on Destroy
        // requires — a call into a destroyed engine through a stale wrapper is undefined behaviour,
        // not an error code.
        _engine = null;
        _delegate = null;

        AgoraRtcEngineKit.Destroy();

        Report("destroyed");
    }

    private static void AssertZero(nint code, string what)
    {
        Assert(code == 0, $"'{what}' returned {code} rather than 0.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// A subclass of the bound (all-optional) delegate protocol. Its existence at engine
    /// construction is the assertion; no callback in the bound surface fires without a join.
    /// </summary>
    private sealed class EngineDelegate : AgoraRtcEngineDelegate
    {
        public override void DidOccurError(AgoraRtcEngineKit engine, AgoraErrorCode errorCode) =>
            SmokeTests.Reporter($"delegate: error {errorCode}");
    }
}
