#!/usr/bin/env bash
set -euo pipefail

# Selects the Xcode that carries the macOS SDK our net10 target framework is built against.
#
# Usage: select-xcode.sh [macos-sdk-version]     # defaults to 26.0
#
# WHY THIS IS PINNED
# The packages advertise net10.0-macos26.0, and .NET for macOS will only build that target
# framework against an Xcode carrying the matching macOS SDK:
#
#     error : This version of .NET for macOS (26.0.9783) requires Xcode 26.0.
#             The current version of Xcode is 26.5.
#
# A runner image's default Xcode can be newer than that, so without this step the build cannot
# produce the very target framework the package ships. Selecting it explicitly also means an image
# update that moves the default Xcode cannot silently change what the build produces.
#
# Resolved by glob rather than a hard-coded /Applications/Xcode_26.0.app: images carry patch
# releases and a hard-coded path silently goes stale when they re-roll. Any 26.0.x carries the
# macOS 26.0 SDK, which is what the target framework needs.

MACOS_SDK_VERSION="${1:-26.0}"

# Newest patch release first, so 26.0.10 would win over 26.0.9 rather than sorting lexically.
XCODE_APP="$(ls -d "/Applications/Xcode_${MACOS_SDK_VERSION}"*.app 2>/dev/null | sort -V | tail -1 || true)"

if [ -z "${XCODE_APP}" ]; then
    echo "::error::no Xcode carrying the macOS ${MACOS_SDK_VERSION} SDK is installed on this runner" >&2
    echo "available:" >&2
    ls -d /Applications/Xcode*.app >&2 || true
    exit 1
fi

sudo xcode-select -s "${XCODE_APP}"

echo "selected ${XCODE_APP}"
xcodebuild -version
