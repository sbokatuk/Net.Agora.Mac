#!/usr/bin/env bash
set -euo pipefail

# Builds the device test app against a packed Net.Agora.<PACKAGE>.Mac package and runs it directly
# on the host Mac. The app prints its verdict to stdout; this script turns that into an exit code.
# Adapted from the iOS repository's run-simulator-tests.sh — the device tests print the same
# AGORA_E2E_DONE marker — but there is no simulator on macOS, so the built .app's binary is executed
# straight on the runner.
#
# Usage: run-macos-tests.sh VERSION [TARGET_FRAMEWORK] [PACKAGE]
#
# PACKAGE is the packages.tsv id — Video (default) or Voice. One run exercises one package: both
# ship a framework named AgoraRtcKit, so a single app holds exactly one of them.

VERSION="${1:?a package version is required}"
TARGET_FRAMEWORK="${2:-net10.0-macos26.0}"
PACKAGE="${3:-Video}"

LOG_FILE="macos-tests.log"

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PROJECT="${REPO_ROOT}/tests/Net.Agora.Mac.DeviceTests/Net.Agora.Mac.DeviceTests.csproj"

# The .NET 9 band builds net8/net9 and the .NET 10 band builds net9/net10, so pick the SDK that
# owns the requested target framework. The SDK is resolved from the working directory, and the
# repository's global.json pins .NET 9, hence the scratch directory.
case "${TARGET_FRAMEWORK}" in
    net10.0-*) sdk_major=10 ;;
    *)         sdk_major=9 ;;
esac

sdk_version="$(dotnet --list-sdks | grep "^${sdk_major}\." | tail -1 | cut -d' ' -f1)"
if [ -z "${sdk_version}" ]; then
    echo "::error::no .NET ${sdk_major} SDK installed, cannot build ${TARGET_FRAMEWORK}"
    exit 1
fi

SDK_DIR="$(mktemp -d)"
trap 'rm -rf "${SDK_DIR}"' EXIT
printf '{ "sdk": { "version": "%s", "rollForward": "latestFeature" } }\n' "${sdk_version}" \
    > "${SDK_DIR}/global.json"

# NuGet caches by package id + version, so rebuilding a version that was already restored once
# silently reuses the stale copy. CI versions are unique, but locally you will re-pack the same
# version repeatedly and test yesterday's bits without this. Driven by packages.tsv so a package
# added there is purged here without a second edit.
while IFS=$'\t' read -r name _rest; do
    case "${name}" in ''|\#*) continue ;; esac
    lower="$(printf '%s' "${name}" | tr '[:upper:]' '[:lower:]')"
    rm -rf "${HOME}/.nuget/packages/net.agora.${lower}.mac/${VERSION}"
done < "${REPO_ROOT}/build/packages.tsv"

# The app's own intermediate output has to go too, not just the NuGet cache. The native payload is
# extracted out of the package into obj/ and copied into the .app, and neither step re-runs when
# the package version string is unchanged — so a rebuilt package of the same version leaves the
# *previous* build's xcframeworks embedded in the app.
rm -rf "${REPO_ROOT}/tests/Net.Agora.Mac.DeviceTests/obj" \
       "${REPO_ROOT}/tests/Net.Agora.Mac.DeviceTests/bin"

# Local escape hatch: .NET for macOS insists on the exact Xcode its SDK was built against. CI
# selects that Xcode (see .github/scripts/select-xcode.sh), so this is empty there; a developer on
# a newer Xcode can set AGORA_SKIP_XCODE_CHECK=1 to build anyway (the check is major.minor only and
# a newer patch/minor links fine for a smoke run).
XCODE_OVERRIDE=""
if [ -n "${AGORA_SKIP_XCODE_CHECK:-}" ]; then
    XCODE_OVERRIDE="-p:_IsMatchingXcode=true"
fi

echo "==> building device tests (package=Net.Agora.${PACKAGE}.Mac, version=${VERSION}, tfm=${TARGET_FRAMEWORK}, sdk=${sdk_version})"
# Debug, not Release — a Release build AOT-compiles and links every assembly, which costs real
# runner time for no additional signal on whether the package restores, resolves and links, which
# is what this suite verifies.
( cd "${SDK_DIR}" && dotnet build "${PROJECT}" \
    --configuration Debug \
    -p:AgoraDevicePackage="${PACKAGE}" \
    -p:AgoraDevicePackageVersion="${VERSION}" \
    -p:AgoraDeviceTargetFramework="${TARGET_FRAMEWORK}" \
    ${XCODE_OVERRIDE} )

# The .app lands under an architecture-named subfolder (osx-arm64 on Apple silicon runners). Find
# the executable inside it rather than pinning the RID, so the same script works on any runner.
APP_BINARY="$(find "${REPO_ROOT}/tests/Net.Agora.Mac.DeviceTests/bin/Debug/${TARGET_FRAMEWORK}" \
    -type f -path '*.app/Contents/MacOS/*' -perm +111 -print -quit 2>/dev/null || true)"
if [ -z "${APP_BINARY}" ]; then
    echo "::error::no .app bundle was produced"
    exit 1
fi

echo "==> running ${APP_BINARY}"
# The app writes its checks to stdout and exits itself once it has printed AGORA_E2E_DONE. Quieten
# the Mono logger so the stream is the test output rather than runtime chatter.
set +e
MONO_LOG_LEVEL=error "${APP_BINARY}" 2>&1 | tee "${LOG_FILE}"
status=${PIPESTATUS[0]}
set -e

if ! grep -q "AGORA_E2E_DONE PASS" "${LOG_FILE}"; then
    # A missing or mis-stripped xcframework shows up here as a dyld failure naming the framework —
    # video_dec is the historical example, see the NativeReference comment in the Video csproj.
    echo "::error::Agora macOS checks failed or timed out (exit ${status})"
    exit 1
fi

echo "==> macOS checks passed"
