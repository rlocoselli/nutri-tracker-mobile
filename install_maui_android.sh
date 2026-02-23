#!/usr/bin/env bash
set -euo pipefail

DOTNET_CHANNEL="${DOTNET_CHANNEL:-8.0}"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
ANDROID_SDK_ROOT="${ANDROID_SDK_ROOT:-$HOME/.android-sdk}"
ANDROID_HOME="$ANDROID_SDK_ROOT"
TOOLS_ZIP_URL="${TOOLS_ZIP_URL:-https://dl.google.com/android/repository/commandlinetools-linux-11076708_latest.zip}"
TOOLS_ZIP_PATH="${TOOLS_ZIP_PATH:-/tmp/cmdline-tools.zip}"

if [[ "${1:-}" == "--help" ]]; then
  cat <<'EOF'
Usage: scripts/install_maui_android.sh [--smoke-test]

Installs (user-level):
- .NET SDK (channel 8.0 by default)
- Android SDK cmdline-tools + platform-tools + Android API 35 + build-tools 35.0.0
- .NET workloads: maui-android
- MAUI templates

Options:
  --smoke-test   Creates and builds a sample MAUI Android app in /tmp/MauiSmokeTest
EOF
  exit 0
fi

SMOKE_TEST="false"
if [[ "${1:-}" == "--smoke-test" ]]; then
  SMOKE_TEST="true"
fi

log() {
  printf "\n[%s] %s\n" "$(date +'%H:%M:%S')" "$*"
}

ensure_dotnet() {
  if [[ -x "$DOTNET_ROOT/dotnet" ]]; then
    log "dotnet already present: $($DOTNET_ROOT/dotnet --version)"
    return
  fi

  log "Installing .NET SDK channel $DOTNET_CHANNEL in $DOTNET_ROOT"
  mkdir -p "$HOME/.local/bin" "$DOTNET_ROOT"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$HOME/.local/bin/dotnet-install.sh"
  chmod +x "$HOME/.local/bin/dotnet-install.sh"
  "$HOME/.local/bin/dotnet-install.sh" --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_ROOT"
  log "Installed dotnet: $($DOTNET_ROOT/dotnet --version)"
}

install_android_cmdline_tools() {
  log "Installing Android cmdline-tools in $ANDROID_SDK_ROOT"
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
  export ANDROID_SDK_ROOT ANDROID_HOME
  export PATH="$DOTNET_ROOT:$ANDROID_SDK_ROOT/cmdline-tools/latest/bin:$ANDROID_SDK_ROOT/platform-tools:$PATH"

  log "Accepting Android SDK licenses"
  yes | sdkmanager --licenses >/dev/null || true

  log "Installing Android SDK packages"
  sdkmanager "platform-tools" "platforms;android-35" "build-tools;35.0.0"
}

install_maui() {
  export DOTNET_ROOT ANDROID_SDK_ROOT ANDROID_HOME
  export PATH="$DOTNET_ROOT:$ANDROID_SDK_ROOT/cmdline-tools/latest/bin:$ANDROID_SDK_ROOT/platform-tools:$PATH"

  log "Installing maui-android workload"
  "$DOTNET_ROOT/dotnet" workload install maui-android

  log "Installing/updating MAUI templates"
  "$DOTNET_ROOT/dotnet" new install Microsoft.Maui.Templates || true
}

persist_env() {
  if grep -q "AUD_MAUI_SETUP" "$HOME/.bashrc"; then
    log "Environment block already present in ~/.bashrc"
    return
  fi

  log "Persisting environment variables in ~/.bashrc"
  cat >> "$HOME/.bashrc" <<EOF

# AUD_MAUI_SETUP
export DOTNET_ROOT="$DOTNET_ROOT"
export ANDROID_SDK_ROOT="$ANDROID_SDK_ROOT"
export ANDROID_HOME="$ANDROID_HOME"
export PATH="$DOTNET_ROOT:$ANDROID_SDK_ROOT/cmdline-tools/latest/bin:$ANDROID_SDK_ROOT/platform-tools:\$PATH"
# /AUD_MAUI_SETUP
EOF
}

run_smoke_test() {
  export DOTNET_ROOT ANDROID_SDK_ROOT ANDROID_HOME
  export PATH="$DOTNET_ROOT:$ANDROID_SDK_ROOT/cmdline-tools/latest/bin:$ANDROID_SDK_ROOT/platform-tools:$PATH"

  log "Running MAUI Android smoke test"
  rm -rf /tmp/MauiSmokeTest
  "$DOTNET_ROOT/dotnet" new maui -n MauiSmokeTest -o /tmp/MauiSmokeTest
  "$DOTNET_ROOT/dotnet" build /tmp/MauiSmokeTest/MauiSmokeTest.csproj -f net8.0-android -p:TargetFrameworks=net8.0-android -v minimal
  log "Smoke test OK: /tmp/MauiSmokeTest/bin/Debug/net8.0-android"
}

main() {
  ensure_dotnet
  install_android_cmdline_tools
  install_android_packages
  install_maui
  persist_env

  log "Installed versions"
  export DOTNET_ROOT ANDROID_SDK_ROOT ANDROID_HOME
  export PATH="$DOTNET_ROOT:$ANDROID_SDK_ROOT/cmdline-tools/latest/bin:$ANDROID_SDK_ROOT/platform-tools:$PATH"
  "$DOTNET_ROOT/dotnet" --version
  sdkmanager --version || true
  adb --version | head -n 1 || true
  "$DOTNET_ROOT/dotnet" new list maui | sed -n '1,80p' || true

  if [[ "$SMOKE_TEST" == "true" ]]; then
    run_smoke_test
  fi

  log "Done. Open a new shell or run: source ~/.bashrc"
}

main
