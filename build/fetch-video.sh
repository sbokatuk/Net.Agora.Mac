#!/usr/bin/env bash
set -euo pipefail

# Downloads the Agora Video (RTC) macOS SDK and stages the frameworks Net.Agora.Video.Mac needs
# into src/Net.Agora.Video.Mac/lib.
#
# Agora publishes the native macOS SDK the same way as the iOS one — a zip behind the
# download.agora.io release URL — but as a separate build with a single universal
# macos-arm64_x86_64 slice (there is no simulator on macOS). This script downloads exactly that
# zip, without requiring CocoaPods or Ruby.
#
# Usage:
#   ./build/fetch-video.sh            # the version pinned in Directory.Build.props
#   ./build/fetch-video.sh 4.6.2      # an explicit version
#
# Check what is actually published before bumping the pin:
#   curl -s https://trunk.cocoapods.org/api/v1/pods/AgoraRtcEngine_macOS | python3 -m json.tool

cd "$(dirname "$0")"

ROOT="$(cd .. && pwd)"
CHECKSUMS="$ROOT/build/checksums.txt"
DEST="$ROOT/src/Net.Agora.Video.Mac/lib"

VERSION="${1:-}"
if [ -z "${VERSION}" ]; then
    VERSION=$(sed -n 's:.*<AgoraVideoMacVersion>\(.*\)</AgoraVideoMacVersion>.*:\1:p' "$ROOT/Directory.Build.props" | head -1)
fi

if [ -z "${VERSION}" ]; then
    echo "error: could not read AgoraVideoMacVersion from $ROOT/Directory.Build.props" >&2
    exit 1
fi

# The version is interpolated into a URL and a path, so reject anything exotic up front.
case "$VERSION" in
    *[!A-Za-z0-9._-]*)
        echo "error: invalid version '$VERSION'" >&2
        exit 1
        ;;
esac

expected=$(sed -n "s/^AgoraRtcEngine_macOS-$VERSION[[:space:]]\{1,\}\([0-9a-f]\{64\}\).*/\1/p" "$CHECKSUMS" | head -1)
if [ -z "$expected" ]; then
    echo "error: no SHA-256 recorded for AgoraRtcEngine_macOS-$VERSION in $CHECKSUMS" >&2
    echo "       see the instructions at the top of that file for how to add one" >&2
    exit 1
fi

sha256_of() {
    if command -v shasum >/dev/null 2>&1; then
        shasum -a 256 "$1" | cut -d' ' -f1
    else
        sha256sum "$1" | cut -d' ' -f1
    fi
}

echo "Fetching Agora Video macOS SDK $VERSION..."

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

curl -fL -o "$WORK/sdk.zip" "https://download.agora.io/sdk/release/AgoraRtcEngine_macOS-$VERSION.zip"

actual=$(sha256_of "$WORK/sdk.zip")
if [ "$actual" != "$expected" ]; then
    echo "error: checksum mismatch for AgoraRtcEngine_macOS-$VERSION.zip" >&2
    echo "       expected $expected" >&2
    echo "       actual   $actual" >&2
    exit 1
fi

unzip -q "$WORK/sdk.zip" -d "$WORK/extracted"

rm -rf "$DEST"
mkdir -p "$DEST"

# The core RTC surface only: AgoraRtcKit plus the codec/infra dependencies it links against — every
# @rpath entry `otool -L` reports on AgoraRtcKit's own binary, confirmed against the macOS slice
# (aosl, Agorafdkaac, Agoraffmpeg, AgoraSoundTouch, video_dec). The other ~18 xcframeworks in the
# zip are optional feature plugins shipped as the Net.Agora.Extensions.*.Mac packages — see
# build/fetch-extensions.sh. video_enc, video_dec's sibling, is one such plugin: AgoraRtcKit's
# binary does not reference it.
for framework in AgoraRtcKit aosl Agorafdkaac Agoraffmpeg AgoraSoundTouch video_dec; do
    if [ ! -d "$WORK/extracted/$framework.xcframework" ]; then
        echo "error: $framework.xcframework not found in the downloaded SDK" >&2
        exit 1
    fi
    cp -R "$WORK/extracted/$framework.xcframework" "$DEST/"
done

echo "Staged $(ls "$DEST" | wc -l | tr -d ' ') frameworks into $DEST"
