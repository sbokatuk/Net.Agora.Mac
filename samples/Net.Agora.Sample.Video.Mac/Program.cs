using AppKit;
using CoreGraphics;
using Foundation;
using Net.Agora.Video.Mac;

namespace Net.Agora.Sample.Video.Mac;

public static class Program
{
    private static void Main(string[] args)
    {
        NSApplication.Init();
        var app = NSApplication.SharedApplication;
        app.ActivationPolicy = NSApplicationActivationPolicy.Regular;
        app.Delegate = new AppDelegate();
        app.Run();
    }
}

/// <summary>
/// A one-window AppKit app driving <see cref="AgoraRtcEngineKit"/> directly — the raw binding, no
/// cross-platform façade in between. Enter an App ID and channel, press Join: the local camera
/// renders on the left and the first remote user on the right. This is exactly what a native macOS
/// consumer of Net.Agora.Video.Mac wires up.
/// </summary>
public sealed class AppDelegate : NSApplicationDelegate
{
    private NSWindow _window = null!;
    private NSTextField _appId = null!;
    private NSTextField _channel = null!;
    private NSView _localView = null!;
    private NSView _remoteView = null!;
    private NSTextField _status = null!;

    private AgoraRtcEngineKit? _engine;
    private SampleDelegate? _delegate;
    private bool _joined;

    public override void DidFinishLaunching(NSNotification notification)
    {
        _window = new NSWindow(
            new CGRect(0, 0, 900, 560),
            NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Miniaturizable | NSWindowStyle.Resizable,
            NSBackingStore.Buffered,
            deferCreation: false)
        {
            Title = "Agora Video (macOS)",
        };
        _window.Center();

        var content = _window.ContentView!;

        _appId = LabeledField(content, "App ID", 520, placeholder: "your Agora App ID");
        _channel = LabeledField(content, "Channel", 480, placeholder: "channel name");
        _channel.StringValue = "demo";

        var joinButton = new NSButton { Title = "Join", BezelStyle = NSBezelStyle.Rounded, Frame = new CGRect(20, 440, 90, 30) };
        joinButton.Activated += (_, _) => Join();
        content.AddSubview(joinButton);

        var leaveButton = new NSButton { Title = "Leave", BezelStyle = NSBezelStyle.Rounded, Frame = new CGRect(120, 440, 90, 30) };
        leaveButton.Activated += (_, _) => Leave();
        content.AddSubview(leaveButton);

        _status = new NSTextField(new CGRect(230, 445, 640, 20))
        {
            Editable = false, Bezeled = false, DrawsBackground = false, StringValue = "Not joined.",
        };
        content.AddSubview(_status);

        _localView = VideoPane(content, new CGRect(20, 20, 420, 400), "Local");
        _remoteView = VideoPane(content, new CGRect(460, 20, 420, 400), "Remote");

        _window.MakeKeyAndOrderFront(null);
#pragma warning disable CA1422 // ActivateIgnoringOtherApps is obsolete from macOS 14, but its
                               // replacement (NSApplication.Activate) does not exist below this
                               // app's own SupportedOSPlatformVersion of 12.0.
        NSApplication.SharedApplication.ActivateIgnoringOtherApps(true);
#pragma warning restore CA1422
    }

    private void Join()
    {
        if (_joined)
        {
            return;
        }

        var appId = _appId.StringValue.Trim();
        if (string.IsNullOrEmpty(appId))
        {
            _status.StringValue = "Enter an App ID first.";
            return;
        }

        _delegate = new SampleDelegate(this);
        _engine = AgoraRtcEngineKit.SharedEngine(
            new AgoraRtcEngineConfig { AppId = appId, ChannelProfile = AgoraChannelProfile.LiveBroadcasting },
            _delegate);

        _engine.SetClientRole(AgoraClientRole.Broadcaster);
        _engine.EnableVideo();
        _engine.SetupLocalVideo(new AgoraRtcVideoCanvas { Uid = 0, View = _localView });
        _engine.StartPreview();

        var code = _engine.JoinChannel(token: null, _channel.StringValue.Trim(), info: null, uid: 0, joinSuccess: null);
        _status.StringValue = code == 0 ? "Joining…" : $"joinChannel returned {code}";
        _joined = true;
    }

    private void Leave()
    {
        if (!_joined || _engine is null)
        {
            return;
        }

        _engine.StopPreview();
        _engine.SetupLocalVideo(null);
        _engine.LeaveChannel(null);
        AgoraRtcEngineKit.Destroy();
        _engine = null;
        _delegate = null;
        _joined = false;
        _status.StringValue = "Left the channel.";
    }

    internal void OnRemoteUser(uint uid) => BeginInvokeOnMainThread(() =>
    {
        _engine?.SetupRemoteVideo(new AgoraRtcVideoCanvas { Uid = uid, View = _remoteView });
        _status.StringValue = $"Remote user {uid} joined.";
    });

    internal void OnStatus(string message) => BeginInvokeOnMainThread(() => _status.StringValue = message);

    // ---- tiny programmatic-layout helpers -------------------------------------------------

    private static NSTextField LabeledField(NSView parent, string label, nfloat y, string placeholder)
    {
        parent.AddSubview(new NSTextField(new CGRect(20, y, 70, 20))
        {
            StringValue = label, Editable = false, Bezeled = false, DrawsBackground = false,
        });
        var field = new NSTextField(new CGRect(95, y - 2, 300, 24)) { PlaceholderString = placeholder };
        parent.AddSubview(field);
        return field;
    }

    private static NSView VideoPane(NSView parent, CGRect frame, string label)
    {
        var box = new NSView(frame) { WantsLayer = true };
        box.Layer!.BackgroundColor = NSColor.Black.CGColor;
        parent.AddSubview(box);

        parent.AddSubview(new NSTextField(new CGRect(frame.X, frame.Y + frame.Height + 2, 200, 18))
        {
            StringValue = label, Editable = false, Bezeled = false, DrawsBackground = false,
        });
        return box;
    }

    private sealed class SampleDelegate(AppDelegate owner) : AgoraRtcEngineDelegate
    {
        public override void DidJoinedOfUid(AgoraRtcEngineKit engine, nuint uid, nint elapsed) =>
            owner.OnRemoteUser((uint)uid);

        public override void DidJoinChannel(AgoraRtcEngineKit engine, string channel, nuint uid, nint elapsed) =>
            owner.OnStatus($"Joined '{channel}' as {uid}.");

        public override void DidOccurError(AgoraRtcEngineKit engine, AgoraErrorCode errorCode) =>
            owner.OnStatus($"Error: {errorCode}");
    }
}
