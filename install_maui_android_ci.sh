#!/usr/bin/env bash
set -euo pipefail

DOTNET_CHANNEL="${DOTNET_CHANNEL:-8.0}"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
ANDROID_SDK_ROOT="${ANDROID_SDK_ROOT:-$HOME/.android-sdk}"
ANDROID_HOME="$ANDROID_SDK_ROOT"
TOOLS_ZIP_URL="${TOOLS_ZIP_URL:-https://dl.google.com/android/repository/commandlinetools-linux-11076708_latest.zip}"
TOOLS_ZIP_PATH="${TOOLS_ZIP_PATH:-/tmp/cmdline-tools.zip}"
MAUI_CI_SMOKE_TEST="${MAUI_CI_SMOKE_TEST:-1}"
MAUI_WRITE_BASHRC="${MAUI_WRITE_BASHRC:-0}"
LOCK_FILE="${LOCK_FILE:-/tmp/maui-android-install.lock}"

log() {
  printf "[%s] %s\n" "$(date +'%H:%M:%S')" "$*"
}

acquire_lock() {
  exec 9>"$LOCK_FILE"
  if command -v flock >/dev/null 2>&1; then
    if ! flock -n 9; then
      log "Another MAUI install is already running (lock: $LOCK_FILE)."
      exit 1
    fi
  fi
}

ensure_dotnet() {
  if [[ -x "$DOTNET_ROOT/dotnet" ]]; then
    log "dotnet already present: $($DOTNET_ROOT/dotnet --version)"
    return
  fi

  log "Installing dotnet SDK channel $DOTNET_CHANNEL"
  mkdir -p "$HOME/.local/bin" "$DOTNET_ROOT"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$HOME/.local/bin/dotnet-install.sh"
  chmod +x "$HOME/.local/bin/dotnet-install.sh"
  "$HOME/.local/bin/dotnet-install.sh" --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_ROOT"
  log "dotnet installed: $($DOTNET_ROOT/dotnet --version)"
}

install_cmdline_tools() {
  if [[ -x "$ANDROID_SDK_ROOT/cmdline-tools/latest/bin/sdkmanager" ]]; then
    log "Android cmdline-tools already present"
    return
  fi

  log "Installing Android cmdline-tools"
  mkdir -p "$ANDROID_SDK_ROOT/cmdline-tools"
  curl -fL "$TOOLS_ZIP_URL" -o "$TOOLS_ZIP_PATH"

  python3 - <<PY
import os, zipfile, shutil
sdk_root = os.path.expanduser("$ANDROID_SDK_ROOT")
zip_path = "$TOOLS_ZIP_PATH"
extract_dir = "/tmp/android-cmdline-extract"
if os.path.exists(extract_dir):
    shutil.rmtree(extract_dir)
os.makedirs(extract_dir, exist_ok=True)
with zipfile.ZipFile(zip_path, 'r') as z:
    z.extractall(extract_dir)
src = os.path.join(extract_dir, 'cmdline-tools')
dst = os.path.join(sdk_root, 'cmdline-tools', 'latest')
if os.path.exists(dst):
    shutil.rmtree(dst)
shutil.move(src, dst)
print(dst)
PY

  chmod +x "$ANDROID_SDK_ROOT/cmdline-tools/latest/bin/"* || true
}

install_android_packages() {
  export DOTNET_ROOT ANDROID_SDK_ROOT ANDROID_HOME
  export PATH="$DOTNET_ROOT:$ANDROID_SDK_ROOT/cmdline-tools/latest/bin:$ANDROID_SDK_ROOT/platform-tools:$PATH"

  log "Accepting Android SDK licenses"
  yes | sdkmanager --licenses >/dev/null || true

  log "Installing Android SDK packages"
  sdkmanager --install \
    "platform-tools" \
    "platforms;android-35" \
    "build-tools;35.0.0" >/dev/null
}

install_maui_android() {
  export DOTNET_ROOT ANDROID_SDK_ROOT ANDROID_HOME
  export PATH="$DOTNET_ROOT:$ANDROID_SDK_ROOT/cmdline-tools/latest/bin:$ANDROID_SDK_ROOT/platform-tools:$PATH"

  log "Installing maui-android workload"
  "$DOTNET_ROOT/dotnet" workload install maui-android --skip-manifest-update --verbosity minimal

  log "Installing MAUI templates"
  "$DOTNET_ROOT/dotnet" new install Microsoft.Maui.Templates >/dev/null 2>&1 || true
}

write_bashrc() {
  if [[ "$MAUI_WRITE_BASHRC" != "1" ]]; then
    return
  fi

  if grep -q "AUD_MAUI_SETUP" "$HOME/.bashrc"; then
    log "~/.bashrc already contains MAUI block"
    return
  fi

  log "Writing MAUI env block to ~/.bashrc"
  cat >> "$HOME/.bashrc" <<EOF

# AUD_MAUI_SETUP
export DOTNET_ROOT="$DOTNET_ROOT"
export ANDROID_SDK_ROOT="$ANDROID_SDK_ROOT"
export ANDROID_HOME="$ANDROID_HOME"
export PATH="$DOTNET_ROOT:$ANDROID_SDK_ROOT/cmdline-tools/latest/bin:$ANDROID_SDK_ROOT/platform-tools:\$PATH"
# /AUD_MAUI_SETUP
EOF
}

smoke_test() {
  if [[ "$MAUI_CI_SMOKE_TEST" != "1" ]]; then
    log "Smoke test skipped (MAUI_CI_SMOKE_TEST=$MAUI_CI_SMOKE_TEST)"
    return
  fi

  export DOTNET_ROOT ANDROID_SDK_ROOT ANDROID_HOME
  export PATH="$DOTNET_ROOT:$ANDROID_SDK_ROOT/cmdline-tools/latest/bin:$ANDROID_SDK_ROOT/platform-tools:$PATH"

  log "Running MAUI Android smoke test"
  rm -rf /tmp/MauiSmokeTest
  "$DOTNET_ROOT/dotnet" new maui -n MauiSmokeTest -o /tmp/MauiSmokeTest >/dev/null
  "$DOTNET_ROOT/dotnet" build /tmp/MauiSmokeTest/MauiSmokeTest.csproj \
    -f net8.0-android \
    -p:TargetFrameworks=net8.0-android \
    -v q

  if [[ -f "/tmp/MauiSmokeTest/bin/Debug/net8.0-android/com.companyname.mauismoketest.apk" ]]; then
    log "Smoke test OK: APK generated"
  else
    log "Smoke test completed but APK not found"
    exit 1
  fi
}

main() {
  acquire_lock
  ensure_dotnet
  install_cmdline_tools
  install_android_packages
  install_maui_android
  write_bashrc
  smoke_test

  export DOTNET_ROOT ANDROID_SDK_ROOT ANDROID_HOME
  export PATH="$DOTNET_ROOT:$ANDROID_SDK_ROOT/cmdline-tools/latest/bin:$ANDROID_SDK_ROOT/platform-tools:$PATH"

  log "dotnet: $($DOTNET_ROOT/dotnet --version)"
  log "sdkmanager: $(sdkmanager --version | tr -d '\r' | head -n 1)"
  log "adb: $(adb --version | head -n 1)"
  log "MAUI setup completed"
}

main
