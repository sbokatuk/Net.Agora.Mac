#!/usr/bin/env bash
set -euo pipefail

# Stages the frameworks Net.Agora.Voice.Mac needs into src/Net.Agora.Voice.Mac/lib.
#
# Unlike the iOS repository, there is no standalone voice-only macOS SDK to download: Agora ships
# no current AgoraAudio_macOS (the pod is a dead 3.7.x line), so the Voice package binds the audio
# surface of the *same* AgoraRtcEngine_macOS archive the Video package uses, and stages the same
# six frameworks. Its value is the audio-only managed API and façade parity, not a smaller payload
# — see src/Net.Agora.Voice.Mac/Net.Agora.Voice.Mac.csproj.
#
# Because it is the same archive as fetch-video.sh, there is nothing new to pin in
# build/checksums.txt — the AgoraRtcEngine_macOS hash already recorded there covers it.
#
# Usage:
#   ./build/fetch-voice.sh            # the version pinned in Directory.Build.props
#   ./build/fetch-voice.sh 4.6.2      # an explicit version

cd "$(dirname "$0")"

ROOT="$(cd .. && pwd)"
CHECKSUMS="$ROOT/build/checksums.txt"
DEST="$ROOT/src/Net.Agora.Voice.Mac/lib"

VERSION="${1:-}"
if [ -z "${VERSION}" ]; then
    VERSION=$(sed -n 's:.*<AgoraVoiceMacVersion>\(.*\)</AgoraVoiceMacVersion>.*:\1:p' "$ROOT/Directory.Build.props" | head -1)
fi

if [ -z "${VERSION}" ]; then
    echo "error: could not read AgoraVoiceMacVersion from $ROOT/Directory.Build.props" >&2
    exit 1
fi

case "$VERSION" in
    *[!A-Za-z0-9._-]*)
        echo "error: invalid version '$VERSION'" >&2
        exit 1
        ;;
esac

expected=$(sed -n "s/^AgoraRtcEngine_macOS-$VERSION[[:space:]]\{1,\}\([0-9a-f]\{64\}\).*/\1/p" "$CHECKSUMS" | head -1)
if [ -z "$expected" ]; then
    echo "error: no SHA-256 recorded for AgoraRtcEngine_macOS-$VERSION in $CHECKSUMS" >&2
    echo "       (Voice shares the Video archive — record the Video SDK's hash there)" >&2
    exit 1
fi

sha256_of() {
    if command -v shasum >/dev/null 2>&1; then
        shasum -a 256 "$1" | cut -d' ' -f1
    else
        sha256sum "$1" | cut -d' ' -f1
    fi
}

echo "Fetching Agora macOS RTC SDK $VERSION for its audio surface (Voice)..."

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

# The same six frameworks as Video: the macOS AgoraRtcKit is the full engine and loads video_dec
# at launch, so an audio-only app on macOS still has to carry it.
for framework in AgoraRtcKit aosl Agorafdkaac Agoraffmpeg AgoraSoundTouch video_dec; do
    if [ ! -d "$WORK/extracted/$framework.xcframework" ]; then
        echo "error: $framework.xcframework not found in the downloaded SDK" >&2
        exit 1
    fi
    cp -R "$WORK/extracted/$framework.xcframework" "$DEST/"
done

echo "Staged $(ls "$DEST" | wc -l | tr -d ' ') frameworks into $DEST"
