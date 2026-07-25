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
# AgoraRtmKit carries the SDK; aosl is its one @rpath dependency, on its own version line (the RTM
# aosl, not the RTC one). Both hashes are pinned in build/checksums.txt under their exact zip base
# names.
#
# Usage:
#   ./build/fetch-signaling.sh            # the version pinned in Directory.Build.props
#   ./build/fetch-signaling.sh 2.2.8      # an explicit RTM version
#
# Check what is actually published before bumping the pin (the Package.swift is the source of truth
# for the matching aosl version):
#   curl -s https://raw.githubusercontent.com/AgoraIO/AgoraRtm_Apple/main/Package.swift

cd "$(dirname "$0")"

ROOT="$(cd .. && pwd)"
CHECKSUMS="$ROOT/build/checksums.txt"
DEST="$ROOT/src/Net.Agora.Signaling.Mac/lib"

VERSION="${1:-}"
if [ -z "${VERSION}" ]; then
    VERSION=$(sed -n 's:.*<AgoraSignalingMacVersion>\(.*\)</AgoraSignalingMacVersion>.*:\1:p' "$ROOT/Directory.Build.props" | head -1)
fi

if [ -z "${VERSION}" ]; then
    echo "error: could not read AgoraSignalingMacVersion from $ROOT/Directory.Build.props" >&2
    exit 1
fi

# The aosl version that pairs with this RTM release. Not derivable from the RTM version — it is the
# one the AgoraRtm_Apple Package.swift pins alongside AgoraRtmKit for this tag.
AOSL_VERSION="1.3.0"

case "$VERSION" in
    *[!A-Za-z0-9._-]*)
        echo "error: invalid version '$VERSION'" >&2
        exit 1
        ;;
esac

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

echo "Fetching Agora Signaling macOS frameworks (RTM $VERSION, aosl $AOSL_VERSION)..."

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
mkdir -p "$WORK/extracted"

fetch_xcframework AgoraRtmKit "$VERSION" "$WORK"
fetch_xcframework aosl "$AOSL_VERSION" "$WORK"

rm -rf "$DEST"
mkdir -p "$DEST"

for framework in AgoraRtmKit aosl; do
    if [ ! -d "$WORK/extracted/$framework.xcframework" ]; then
        echo "error: $framework.xcframework not found in the downloaded SDK" >&2
        exit 1
    fi
    cp -R "$WORK/extracted/$framework.xcframework" "$DEST/"
done

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

echo "Staged $(ls "$DEST" | wc -l | tr -d ' ') frameworks into $DEST"
