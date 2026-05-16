#!/usr/bin/env bash

set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ADB_BIN="${ADB_BIN:-${ANDROID_SDK_ROOT:-$HOME/.android-sdk}/platform-tools/adb}"
PACKAGE_NAME="${PACKAGE_NAME:-com.audela.nutritiontracker}"
DEVICE_SERIAL="${1:-}"

if [[ ! -x "$ADB_BIN" ]]; then
  if command -v adb >/dev/null 2>&1; then
    ADB_BIN="$(command -v adb)"
  else
    echo "ERROR: adb not found. Install Android platform-tools or set ADB_BIN." >&2
    exit 1
  fi
fi

pick_connected_device() {
  "$ADB_BIN" devices -l | awk 'NR > 1 && $2 == "device" {print $1; exit}'
}

if [[ -z "$DEVICE_SERIAL" ]]; then
  DEVICE_SERIAL="$(pick_connected_device)"
fi

if [[ -z "$DEVICE_SERIAL" ]]; then
  echo "ERROR: no connected Android device found." >&2
  "$ADB_BIN" devices -l || true
  exit 1
fi

mapfile -t APK_CANDIDATES < <(
  find "$PROJECT_ROOT/bin/Release" -type f -name "*Signed.apk" -print 2>/dev/null | sort
)

if [[ ${#APK_CANDIDATES[@]} -eq 0 ]]; then
  mapfile -t APK_CANDIDATES < <(
    find "$PROJECT_ROOT/bin" -type f -name "*Signed.apk" -print 2>/dev/null | sort
  )
fi

if [[ ${#APK_CANDIDATES[@]} -eq 0 ]]; then
  echo "ERROR: no signed APK found under $PROJECT_ROOT/bin" >&2
  exit 1
fi

LATEST_APK="$(ls -t "${APK_CANDIDATES[@]}" | head -n 1)"

echo "Using device: $DEVICE_SERIAL"
echo "Using APK: $LATEST_APK"
ls -l --time-style=long-iso "$LATEST_APK"

"$ADB_BIN" -s "$DEVICE_SERIAL" install -r "$LATEST_APK"

echo "Installed package info:"
"$ADB_BIN" -s "$DEVICE_SERIAL" shell dumpsys package "$PACKAGE_NAME" | grep -E "versionName|versionCode|lastUpdateTime" || true
