#!/usr/bin/env bash
# The only parser of Directory.Build.props for shell callers. Source this, don't execute it.
#
#   . build/pins.sh
#   echo "$AGORA_VIDEO_MAC_VERSION"

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export AGORA_REPO_ROOT
AGORA_REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

_prop() {
    grep -oE "<$1>[^<]+" "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed "s/<$1>//"
}

export AGORA_VIDEO_MAC_VERSION
AGORA_VIDEO_MAC_VERSION="$(_prop AgoraVideoMacVersion)"
export AGORA_VIDEO_BINDING_REVISION
AGORA_VIDEO_BINDING_REVISION="$(_prop AgoraVideoBindingRevision)"
export AGORA_VIDEO_PACKAGE_VERSION="${AGORA_VIDEO_MAC_VERSION}.${AGORA_VIDEO_BINDING_REVISION}"

export AGORA_VOICE_MAC_VERSION
AGORA_VOICE_MAC_VERSION="$(_prop AgoraVoiceMacVersion)"
export AGORA_VOICE_BINDING_REVISION
AGORA_VOICE_BINDING_REVISION="$(_prop AgoraVoiceBindingRevision)"
export AGORA_VOICE_PACKAGE_VERSION="${AGORA_VOICE_MAC_VERSION}.${AGORA_VOICE_BINDING_REVISION}"

export AGORA_SIGNALING_MAC_VERSION
AGORA_SIGNALING_MAC_VERSION="$(_prop AgoraSignalingMacVersion)"
export AGORA_SIGNALING_BINDING_REVISION
AGORA_SIGNALING_BINDING_REVISION="$(_prop AgoraSignalingBindingRevision)"
export AGORA_SIGNALING_PACKAGE_VERSION="${AGORA_SIGNALING_MAC_VERSION}.${AGORA_SIGNALING_BINDING_REVISION}"

# No Whiteboard on macOS — netless ships no macOS/AppKit build; see Directory.Build.props.
