#!/usr/bin/env bash
set -euo pipefail

# Downloads the Agora Signaling (RTM 2.x) macOS frameworks and stages them into
# src/Net.Agora.Signaling.Mac/lib.
#
# Unlike the iOS repository — which downloads a single AgoraRtm_iOS SDK zip — the RTM 2.x SDK for
# Apple platforms is distributed as the AgoraRtm_Apple Swift package's two binary xcframeworks,
# each its own zip behind download.agora.io/rtm2/release/. They ship one universal xcframework
# spanning iOS, macOS and visionOS; this script keeps only the macos-arm64_x86_64 slice.
#
# AgoraRtmKit carries the SDK; aosl is its one @rpath dependency — but it comes from the RTC macOS
# SDK archive, not from the aosl.xcframework_<version>.zip sitting beside AgoraRtmKit on the same
# server, which is the pairing AgoraRtm_Apple's Package.swift declares.
#
# Why: every package here that ships an aosl.xcframework puts it at the same path,
# Contents/Frameworks/aosl.framework, in a consuming app's bundle. The bundle carries one copy —
# whichever the build kept — so every product in that app runs against it. The RTM-paired aosl is
# 1.3.0 and the RTC SDK's is 1.3.5, and 1.3.0 does not export _aosl_data_ptr, one of the 144 aosl
# symbols AgoraRtcKit resolves at load. An app referencing Signaling and Video or Voice could
# therefore fail to load the RTC engine, with nothing in the build to say so. Taking aosl from the
# RTC archive makes the two copies the same file, so there is nothing left to win. The reverse
# direction was checked before the switch: all 116 aosl symbols AgoraRtmKit imports are exported by
# 1.3.5 as well, so Signaling on its own is unaffected.
#
# Agora publishes no standalone aosl 1.3.x for macOS — only the 1.3.0 on the rtm2 path — so the
# whole RTC SDK archive is the only place to get it. That is why this script reads
# AgoraVideoMacVersion: it needs the archive the RTC packages ship aosl from, not a version of its
# own. tests/Net.Agora.Mac.PackageTests asserts the result — that the aosl this package packs is
# byte for byte the one Net.Agora.Video.Mac packs.
#
# Usage:
#   ./build/fetch-signaling.sh              # the versions pinned in Directory.Build.props
#   ./build/fetch-signaling.sh 2.2.8        # an explicit RTM version, pinned RTC version
#   ./build/fetch-signaling.sh 2.2.8 4.6.2  # both explicit
#
# Check what is actually published before bumping the RTM pin (the Package.swift is the source of
# truth for the RTM release's own aosl pairing, which this script deliberately does not follow):
#   curl -s https://raw.githubusercontent.com/AgoraIO/AgoraRtm_Apple/main/Package.swift

cd "$(dirname "$0")"

ROOT="$(cd .. && pwd)"
CHECKSUMS="$ROOT/build/checksums.txt"
DEST="$ROOT/src/Net.Agora.Signaling.Mac/lib"

read_pin() {
    sed -n "s:.*<$1>\(.*\)</$1>.*:\1:p" "$ROOT/Directory.Build.props" | head -1
}

VERSION="${1:-}"
if [ -z "${VERSION}" ]; then
    VERSION=$(read_pin AgoraSignalingMacVersion)
fi

# The RTC SDK release aosl is taken from — see the header. Deliberately the Video pin rather than a
# version of its own: the point is to ship the aosl the RTC packages ship, so there is no second
# number here that could drift away from it.
RTC_VERSION="${2:-}"
if [ -z "${RTC_VERSION}" ]; then
    RTC_VERSION=$(read_pin AgoraVideoMacVersion)
fi

if [ -z "${VERSION}" ] || [ -z "${RTC_VERSION}" ]; then
    echo "error: could not read AgoraSignalingMacVersion / AgoraVideoMacVersion from" >&2
    echo "       $ROOT/Directory.Build.props" >&2
    exit 1
fi

for v in "$VERSION" "$RTC_VERSION"; do
    case "$v" in
        *[!A-Za-z0-9._-]*)
            echo "error: invalid version '$v'" >&2
            exit 1
            ;;
    esac
done

sha256_of() {
    if command -v shasum >/dev/null 2>&1; then
        shasum -a 256 "$1" | cut -d' ' -f1
    else
        sha256sum "$1" | cut -d' ' -f1
    fi
}

# Download one <artifact>.xcframework_<version>.zip, verify it against build/checksums.txt (keyed
# by the zip's base name), and unzip it into $2.
fetch_xcframework() {
    artifact="$1"       # e.g. AgoraRtmKit
    version="$2"        # e.g. 2.2.8
    into="$3"
    key="$artifact.xcframework_$version"

    expected=$(sed -n "s/^$key[[:space:]]\{1,\}\([0-9a-f]\{64\}\).*/\1/p" "$CHECKSUMS" | head -1)
    if [ -z "$expected" ]; then
        echo "error: no SHA-256 recorded for $key in $CHECKSUMS" >&2
        exit 1
    fi

    curl -fL -o "$into/$key.zip" "https://download.agora.io/rtm2/release/$key.zip"

    actual=$(sha256_of "$into/$key.zip")
    if [ "$actual" != "$expected" ]; then
        echo "error: checksum mismatch for $key.zip" >&2
        echo "       expected $expected" >&2
        echo "       actual   $actual" >&2
        exit 1
    fi

    unzip -q "$into/$key.zip" -d "$into/extracted"
}

echo "Fetching Agora Signaling macOS frameworks (RTM $VERSION, aosl from RTC $RTC_VERSION)..."

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
mkdir -p "$WORK/extracted" "$WORK/rtc"

fetch_xcframework AgoraRtmKit "$VERSION" "$WORK"

# aosl, out of the RTC SDK archive — the header says why it is not the aosl.xcframework zip beside
# AgoraRtmKit. Verified against the same build/checksums.txt entry fetch-video.sh uses, so the two
# scripts cannot end up staging aosl from differently-hashed downloads.
rtc_key="AgoraRtcEngine_macOS-$RTC_VERSION"
rtc_expected=$(sed -n "s/^$rtc_key[[:space:]]\{1,\}\([0-9a-f]\{64\}\).*/\1/p" "$CHECKSUMS" | head -1)
if [ -z "$rtc_expected" ]; then
    echo "error: no SHA-256 recorded for $rtc_key in $CHECKSUMS" >&2
    exit 1
fi

curl -fL -o "$WORK/rtc.zip" "https://download.agora.io/sdk/release/$rtc_key.zip"

rtc_actual=$(sha256_of "$WORK/rtc.zip")
if [ "$rtc_actual" != "$rtc_expected" ]; then
    echo "error: checksum mismatch for $rtc_key.zip" >&2
    echo "       expected $rtc_expected" >&2
    echo "       actual   $rtc_actual" >&2
    exit 1
fi

# Just aosl out of it — the rest of that archive is the RTC surface and its optional plugins, which
# are Net.Agora.Video.Mac's and Net.Agora.Extensions.*.Mac's payloads, not this package's.
unzip -q "$WORK/rtc.zip" 'aosl.xcframework/*' -d "$WORK/rtc"

rm -rf "$DEST"
mkdir -p "$DEST"

if [ ! -d "$WORK/extracted/AgoraRtmKit.xcframework" ]; then
    echo "error: AgoraRtmKit.xcframework not found in the downloaded SDK" >&2
    exit 1
fi
if [ ! -d "$WORK/rtc/aosl.xcframework" ]; then
    echo "error: aosl.xcframework not found in $rtc_key.zip" >&2
    exit 1
fi

cp -R "$WORK/extracted/AgoraRtmKit.xcframework" "$DEST/"
cp -R "$WORK/rtc/aosl.xcframework" "$DEST/"

# The RTM xcframeworks ship iOS, visionOS (xros) and macOS slices. This repository targets
# net*-macos only, so every slice but macos-arm64_x86_64 is stripped: they would be invisible dead
# weight in every consumer's restore, and the package tests hold the payload to a single macOS
# slice. The xcframework's Info.plist has to agree with what is on disk (the Apple SDK rejects a
# manifest that advertises a missing slice), so the matching AvailableLibraries entries are pruned
# too.
python3 - "$DEST" <<'PYEOF'
import plistlib, shutil, sys
from pathlib import Path

dest = Path(sys.argv[1])
for manifest in dest.glob("*.xcframework/Info.plist"):
    bundle = manifest.parent
    with open(manifest, "rb") as f:
        plist = plistlib.load(f)

    kept = []
    for library in plist["AvailableLibraries"]:
        identifier = library["LibraryIdentifier"]
        if identifier.startswith("macos"):
            kept.append(library)
        else:
            shutil.rmtree(bundle / identifier, ignore_errors=True)
    plist["AvailableLibraries"] = kept

    with open(manifest, "wb") as f:
        plistlib.dump(plist, f)
    print(f"{bundle.name}: kept {', '.join(l['LibraryIdentifier'] for l in kept)}")
PYEOF

# The whole reason aosl comes from the RTC archive, checked rather than assumed: a revert to the
# rtm2 aosl would reintroduce exactly the conflict this avoids, in a package that looks entirely
# normal. One binary at a time — `nm` exits non-zero on a non-Mach-O file and `set -o pipefail`
# would then hide a perfectly good grep match behind it.
for binary in "$DEST"/aosl.xcframework/*/aosl.framework/Versions/A/aosl; do
    symbols=$(nm -gU "$binary" 2>/dev/null || true)
    case "$symbols" in
        *_aosl_data_ptr*) ;;
        *)
            echo "error: $binary does not export _aosl_data_ptr, so AgoraRtcKit cannot load" >&2
            echo "       against it. That is the RTM-paired aosl 1.3.0, not the one inside" >&2
            echo "       AgoraRtcEngine_macOS-$RTC_VERSION.zip. See the header of this script." >&2
            exit 1
            ;;
    esac
done

echo "Staged $(ls "$DEST" | wc -l | tr -d ' ') frameworks into $DEST"
