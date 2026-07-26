using System.IO.Compression;

namespace Net.Agora.Mac.PackageTests;

/// <summary>Locates the packed .nupkg files and describes what each is expected to contain.</summary>
public static class Packages
{
    public const string Video = "Net.Agora.Video.Mac";
    public const string Voice = "Net.Agora.Voice.Mac";
    public const string Signaling = "Net.Agora.Signaling.Mac";

    /// <summary>
    /// Every product package build/packages.tsv lists, with the xcframeworks it ships: the API
    /// surface (AgoraRtcKit / AgoraRtmKit) plus the codec and infrastructure frameworks its binary
    /// links against. Mirrors each project's NativeReference list and its fetch script's staging
    /// loop — a name missing from either shows up here. Pinned rather than parsed from the .tsv: a
    /// package silently dropped from the .tsv (and so from the pack) is a regression these tests
    /// should catch, not adapt to.
    /// </summary>
    /// <remarks>
    /// Unlike the iOS repository, Voice carries the same six frameworks as Video — including
    /// video_dec. Agora ships no separate voice-only macOS build, so Voice binds the full macOS
    /// AgoraRtcKit (whose binary loads video_dec at launch) and merely exposes an audio-only
    /// managed surface; see src/Net.Agora.Voice.Mac's csproj.
    /// </remarks>
    public static readonly (string Id, string[] XcFrameworks, long MinPayloadBytes)[] All =
    [
        (Video, ["AgoraRtcKit", "aosl", "Agorafdkaac", "Agoraffmpeg", "AgoraSoundTouch", "video_dec"], 10_000_000),
        (Voice, ["AgoraRtcKit", "aosl", "Agorafdkaac", "Agoraffmpeg", "AgoraSoundTouch", "video_dec"], 10_000_000),
        // Signaling is a smaller product: two frameworks, and the fetch script strips the iOS and
        // visionOS slices the RTM xcframeworks also carry, keeping only macOS.
        (Signaling, ["AgoraRtmKit", "aosl"], 4_000_000),
    ];

    public static string[] XcFrameworksOf(string packageId) =>
        All.Single(p => p.Id == packageId).XcFrameworks;

    public static long MinPayloadBytesOf(string packageId) =>
        All.Single(p => p.Id == packageId).MinPayloadBytes;

    /// <summary>
    /// The optional RTC feature extensions, with the xcframework(s) each must carry. Deliberately
    /// a separate table from <see cref="All"/>: these packages bind nothing (their ApiDefinition.cs
    /// files are empty on purpose — see src/Agora.Extension.md), so the payload-size floors that
    /// exist to catch a truncated SDK download would be beside the point. What matters is that the
    /// framework named here is the one that actually shipped, since the mapping from a package's
    /// name to Agora's framework name is not derivable and lives in build/fetch-extensions.sh.
    /// </summary>
    public static readonly (string Id, string[] XcFrameworks)[] Extensions =
    [
        ("Net.Agora.Extensions.Ains.Mac", ["AgoraAiNoiseSuppressionExtension"]),
        ("Net.Agora.Extensions.Aiaec.Mac", ["AgoraAiEchoCancellationExtension"]),
        ("Net.Agora.Extensions.AudioBeauty.Mac", ["AgoraAudioBeautyExtension"]),
        ("Net.Agora.Extensions.SpatialAudio.Mac", ["AgoraSpatialAudioExtension"]),
        ("Net.Agora.Extensions.VirtualBackground.Mac", ["AgoraVideoSegmentationExtension"]),
        ("Net.Agora.Extensions.ContentInspect.Mac", ["AgoraContentInspectExtension"]),
        ("Net.Agora.Extensions.ClearVision.Mac", ["AgoraClearVisionExtension"]),
        ("Net.Agora.Extensions.FaceCapture.Mac", ["AgoraFaceCaptureExtension"]),
        ("Net.Agora.Extensions.FaceDetection.Mac", ["AgoraFaceDetectionExtension"]),
        ("Net.Agora.Extensions.VideoQualityAnalyzer.Mac", ["AgoraVideoQualityAnalyzerExtension"]),
        // The only two-framework extension: the encoder plugin and the codec it wraps.
        ("Net.Agora.Extensions.VideoEncoder.Mac", ["AgoraVideoEncoderExtension", "video_enc"]),
        ("Net.Agora.Extensions.Av1Encoder.Mac", ["AgoraVideoAv1EncoderExtension"]),
    ];

    /// <summary>Every (extension package, target framework) pair.</summary>
    public static IEnumerable<object[]> ExtensionFrameworks =>
        Extensions.SelectMany(e => TargetFrameworks.Select(tfm => new object[] { e.Id, tfm }));

    /// <summary>Every extension package id.</summary>
    public static IEnumerable<object[]> ExtensionIds =>
        Extensions.Select(e => new object[] { e.Id });

    public static string[] ExtensionXcFrameworksOf(string packageId) =>
        Extensions.Single(e => e.Id == packageId).XcFrameworks;

    /// <summary>
    /// Target frameworks every package here must carry, one per SDK band pass. Pinned rather than
    /// discovered: a package that silently lost a target framework because a pack pass failed is
    /// exactly the regression these tests exist to catch. The macOS workload appends its platform
    /// version to the folder name (net8.0-macos15.0, not a bare net8.0-macos), so the exact
    /// strings the pack produces are pinned here.
    /// </summary>
    public static readonly string[] TargetFrameworks =
    [
        "net8.0-macos15.0", "net9.0-macos15.0", "net10.0-macos26.0",
    ];

    public static IEnumerable<object[]> Frameworks =>
        TargetFrameworks.Select(tfm => new object[] { tfm });

    /// <summary>Every product package id — the axis of the per-package facts.</summary>
    public static IEnumerable<object[]> PackageIds =>
        All.Select(p => new object[] { p.Id });

    /// <summary>Every (package, target framework) pair — the axis most tests run over.</summary>
    public static IEnumerable<object[]> PackageFrameworks =>
        All.SelectMany(p => TargetFrameworks.Select(tfm => new object[] { p.Id, tfm }));

    /// <summary>
    /// The RTC packages only — Video and Voice, the axis of the engine-specific member checks.
    /// Both bind the same AgoraRtcEngineKit class, so what holds for one engine's shape must hold
    /// for the other; Signaling is a different product with no engine at all.
    /// </summary>
    public static IEnumerable<object[]> RtcPackageFrameworks =>
        new[] { Video, Voice }.SelectMany(id => TargetFrameworks.Select(tfm => new object[] { id, tfm }));

    /// <summary>
    /// Whether a slice directory name denotes a macOS slice. There is one macOS slice
    /// (macos-arm64_x86_64 today) and no simulator on macOS, so unlike the iOS suite there is no
    /// device/simulator distinction to draw — only "is this the macOS slice, and the only one".
    /// Checked by shape rather than pinned literally, since the exact string is upstream's to
    /// change.
    /// </summary>
    public static bool IsMacosSlice(string slice) =>
        slice.StartsWith("macos", StringComparison.Ordinal);

    /// <summary>
    /// The binding's native payload entry for a target framework: the xcframeworks, zipped by the
    /// Apple SDK into a single &lt;assembly&gt;.resources.zip beside the binding assembly.
    /// CompressBindingResourcePackage=true in Agora.Binding.props means it is always the zip,
    /// never a loose .resources directory — see the comment there for why.
    /// </summary>
    public static string ResourcesEntry(string packageId, string tfm) =>
        $"lib/{tfm}/{packageId}.resources.zip";

    /// <summary>The names of everything in a binding package's native payload for one target framework.</summary>
    public static IReadOnlyList<string> NativePayload(string packageId, string tfm)
    {
        using var package = OpenPackage(packageId);

        var zipped = package.GetEntry(ResourcesEntry(packageId, tfm));
        if (zipped is null)
        {
            return [];
        }

        using var archive = new ZipArchive(zipped.Open());
        return archive.Entries.Select(e => e.FullName).ToList();
    }

    /// <summary>Reads a package entry fully into memory so it can be seeked.</summary>
    public static MemoryStream ReadEntry(ZipArchive package, string entryName)
    {
        var entry = package.GetEntry(entryName);
        Assert.True(entry is not null, $"Package has no entry '{entryName}'.");

        var buffer = new MemoryStream();
        using (var stream = entry!.Open())
        {
            stream.CopyTo(buffer);
        }

        buffer.Position = 0;
        return buffer;
    }

    /// <summary>
    /// Opens the binding resource package for a target framework. The native payload is a zip
    /// nested inside the .nupkg, so its contents are only reachable through a second archive.
    /// </summary>
    public static ZipArchive OpenNativePayload(ZipArchive package, string packageId, string tfm) =>
        new(ReadEntry(package, ResourcesEntry(packageId, tfm)));

    public static string ArtifactsDirectory { get; } = ResolveArtifactsDirectory();

    public static string FindPackage(string packageId, string extension = ".nupkg")
    {
        var matches = Directory.Exists(ArtifactsDirectory)
            ? Directory.GetFiles(ArtifactsDirectory, $"{packageId}.*{extension}")
                .Where(f => IsVersionOf(packageId, Path.GetFileName(f), extension))
                .ToArray()
            : [];

        Assert.True(
            matches.Length > 0,
            $"No {packageId}.<version>{extension} found in '{ArtifactsDirectory}'. " +
            "Run build/BuildNugets.sh first.");

        // A rebuilt working copy can leave several versions behind; test the newest.
        return matches.OrderByDescending(File.GetLastWriteTimeUtc).First();
    }

    private static bool IsVersionOf(string packageId, string fileName, string extension)
    {
        var remainder = fileName[(packageId.Length + 1)..^extension.Length];
        return remainder.Length > 0 && char.IsDigit(remainder[0]);
    }

    public static ZipArchive OpenPackage(string packageId, string extension = ".nupkg") =>
        ZipFile.OpenRead(FindPackage(packageId, extension));

    private static string ResolveArtifactsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? AppContext.BaseDirectory;

        return Environment.GetEnvironmentVariable("AGORA_ARTIFACTS") is { Length: > 0 } configured
            ? Path.GetFullPath(configured, root)
            : Path.Combine(root, "artifacts");
    }
}
