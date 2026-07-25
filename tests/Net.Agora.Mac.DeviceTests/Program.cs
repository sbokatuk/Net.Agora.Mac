using AppKit;
using Foundation;

namespace Net.Agora.Mac.DeviceTests;

/// <summary>
/// Host for the on-host checks. Runs every one on launch, reports the outcome to stdout — which
/// the runner script captures straight from the process — and exits with a verdict line the runner
/// greps for.
/// </summary>
public static class Program
{
    private static void Main(string[] args)
    {
        NSApplication.Init();
        var app = NSApplication.SharedApplication;
        // No dock icon or menu bar: this is a headless smoke run driven from a script, not an app
        // a user interacts with.
        app.ActivationPolicy = NSApplicationActivationPolicy.Prohibited;
        app.Delegate = new AppDelegate();
        app.Run();
    }
}

public sealed class AppDelegate : NSApplicationDelegate
{
    public override void DidFinishLaunching(NSNotification notification)
    {
        // On the main thread deliberately: AgoraRtcEngineKit dispatches its delegate callbacks on
        // the main queue, and its own samples create and drive the engine from it. Nothing here
        // blocks long enough to matter — no check joins a channel or touches the network.
        RunAndReport();
    }

    private static void RunAndReport()
    {
        SmokeTests.Reporter = message => Console.WriteLine($"    {message}");

        var failures = 0;

        foreach (var test in SmokeTests.All)
        {
            try
            {
                test.Execute();
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception exception)
            {
                failures++;

                // The type as well as the message: half the interesting failures here are a native
                // call throwing something the binding did not expect, and the message alone rarely
                // says which side of the bridge it came from.
                Console.WriteLine($"FAIL {test.Name}: {exception.GetType().Name}: {exception.Message}");

                if (exception.StackTrace is { } stack)
                {
                    Console.WriteLine(stack);
                }
            }
        }

        // The same marker the iOS repository's and façade repository's device tests print, so this
        // repository's run-macos-tests.sh is a straight adaptation of those rather than a fork with
        // a different grep.
        Console.WriteLine(failures == 0
            ? "AGORA_E2E_DONE PASS"
            : $"AGORA_E2E_DONE FAIL ({failures} failed)");
        Console.Out.Flush();

        // Terminate so the runner returns instead of hanging on the AppKit run loop until its
        // timeout.
        Environment.Exit(failures == 0 ? 0 : 1);
    }
}
