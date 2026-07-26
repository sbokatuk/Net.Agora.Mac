namespace Net.Agora.Mac.PackageTests;

/// <summary>
/// Asserts that the binding assembly inside the package exposes the generated ergonomic surface —
/// the [Async] task methods, the delegate-backed events, and the constructor shape. These are all
/// produced by attributes in each ApiDefinition.cs; an attribute that silently stopped applying
/// would still compile and pack cleanly, so the layout checks in <see cref="PackageLayoutTests"/>
/// cannot catch it.
/// </summary>
/// <remarks>
/// The assembly is read with the metadata reader (<see cref="AssemblyApi"/>) rather than loaded:
/// it targets *-macos and references Microsoft.macOS, so it cannot be loaded into this plain
/// net9.0 test process.
/// </remarks>
public class BindingApiTests
{
    private static AssemblyApi OpenBinding(string packageId, string tfm)
    {
        using var package = Packages.OpenPackage(packageId);
        var assembly = Packages.ReadEntry(package, $"lib/{tfm}/{packageId}.dll");
        return new AssemblyApi(assembly);
    }

    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void RtmKit_exposes_the_awaitable_operation_surface(string tfm)
    {
        using var api = OpenBinding(Packages.Signaling, tfm);

        var methods = api.MethodsOf($"{Packages.Signaling}.AgoraRtmClientKit");

        // One *Async per completion-block method — what [Async (ResultTypeName =
        // "AgoraRtmOperationResult")] generates in ApiDefinition.cs. The callback overloads
        // remain beside them, so one of these missing means the attribute fell off, not that
        // the operation moved.
        Assert.Contains("LoginAsync", methods);
        Assert.Contains("LogoutAsync", methods);
        Assert.Contains("RenewTokenAsync", methods);
        Assert.Contains("SubscribeAsync", methods);
        Assert.Contains("UnsubscribeAsync", methods);
        Assert.Contains("PublishAsync", methods);

        // The shared result type the attributes name: generated once, holding the
        // Response/ErrorInfo pair every task resolves to (the task completes rather than faults
        // on an RTM error — see the comment in ApiDefinition.cs).
        Assert.Contains($"{Packages.Signaling}.AgoraRtmOperationResult", api.PublicTypes);
    }

    [Theory]
    [MemberData(nameof(Packages.RtcPackageFrameworks), MemberType = typeof(Packages))]
    public void RtcEngine_exposes_events_and_the_delegate_property(string packageId, string tfm)
    {
        using var api = OpenBinding(packageId, tfm);

        var engine = $"{packageId}.AgoraRtcEngineKit";

        // The generator turns every bound AgoraRtcEngineDelegate callback into an event on the
        // engine. Asserted through callbacks both flavours bind — the video-only ones are
        // deliberately absent from Voice, so they have no place here.
        var events = api.EventsOf(engine);
        Assert.Contains("DidJoinChannel", events);
        Assert.Contains("DidOccurError", events);
        Assert.Contains("ConnectionChangedToState", events);

        // The property pair behind both the events and SharedEngine's second argument. Events
        // install an internal delegate through it, which is also why events and a hand-assigned
        // Delegate are mutually exclusive — see the caveat in ApiDefinition.cs.
        var properties = api.PropertiesOf(engine);
        Assert.Contains("Delegate", properties);
        Assert.Contains("WeakDelegate", properties);
    }

    [Theory]
    [MemberData(nameof(Packages.RtcPackageFrameworks), MemberType = typeof(Packages))]
    public void RtcEngine_has_no_public_parameterless_constructor(string packageId, string tfm)
    {
        using var api = OpenBinding(packageId, tfm);

        // [DisableDefaultCtor] in ApiDefinition.cs, and a deliberate break with earlier packages:
        // `new AgoraRtcEngineKit()` used to compile and hand back a broken non-shared instance.
        // The engine only exists through SharedEngine.
        Assert.DoesNotContain(0, api.PublicConstructorArities($"{packageId}.AgoraRtcEngineKit"));
    }
}
