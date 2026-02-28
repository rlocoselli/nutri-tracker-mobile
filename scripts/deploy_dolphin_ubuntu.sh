
#!/usr/bin/env bash
set -euo pipefail

# Deploy/install Dolphin Emulator on recent Ubuntu versions.
# Usage:
#   bash scripts/deploy_dolphin_ubuntu.sh
#   CHANNEL=dev bash scripts/deploy_dolphin_ubuntu.sh
#   INSTALL_MODE=flatpak bash scripts/deploy_dolphin_ubuntu.sh
#   INSTALL_MODE=auto bash scripts/deploy_dolphin_ubuntu.sh

CHANNEL="${CHANNEL:-stable}" # stable | dev
INSTALL_MODE="${INSTALL_MODE:-auto}" # auto | ppa | flatpak

if [[ "${EUID}" -ne 0 ]]; then
  SUDO="sudo"
else
  SUDO=""
fi

if ! command -v apt >/dev/null 2>&1; then
  echo "This script is intended for Ubuntu/Debian systems with apt."
  exit 1
fi

if [[ "${INSTALL_MODE}" != "auto" && "${INSTALL_MODE}" != "ppa" && "${INSTALL_MODE}" != "flatpak" ]]; then
  echo "INSTALL_MODE must be one of: auto | ppa | flatpak"
  exit 1
fi

echo "[1/6] System info"
source /etc/os-release || true
echo "Detected: ${PRETTY_NAME:-Unknown Linux}"

echo "[2/6] Installing prerequisites"
$SUDO apt update -y
$SUDO apt install -y software-properties-common ca-certificates gnupg curl

install_via_ppa() {
  echo "[3/6] Configuring Dolphin PPA"
  if ! grep -R "dolphin-emu/ppa" /etc/apt/sources.list /etc/apt/sources.list.d 2>/dev/null | grep -q .; then
    $SUDO add-apt-repository -y ppa:dolphin-emu/ppa
  else
    echo "Dolphin PPA already configured"
  fi

  echo "[4/6] Installing Dolphin (${CHANNEL}) via PPA"
  $SUDO apt update -y
  $SUDO apt install -y dolphin-emu
}

install_via_flatpak() {
  echo "[3/6] Installing Flatpak runtime and Flathub remote"
  $SUDO apt update -y
  $SUDO apt install -y flatpak

  if ! flatpak remotes --columns=name | grep -q '^flathub$'; then
    $SUDO flatpak remote-add --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo
  fi

  echo "[4/6] Installing Dolphin via Flatpak"
  $SUDO flatpak install -y flathub org.DolphinEmu.dolphin-emu
}

if [[ "${INSTALL_MODE}" == "flatpak" ]]; then
  install_via_flatpak
elif [[ "${INSTALL_MODE}" == "ppa" ]]; then
  install_via_ppa
else
  echo "[3/6] INSTALL_MODE=auto -> trying PPA first"
  if install_via_ppa; then
    echo "PPA install succeeded"
  else
    echo "PPA install failed, falling back to Flatpak"
    install_via_flatpak
  fi
fi

echo "[5/6] Verifying installation"
if command -v dolphin-emu >/dev/null 2>&1; then
  dolphin-emu --version || true
elif command -v flatpak >/dev/null 2>&1 && flatpak list --app --columns=application | grep -q '^org.DolphinEmu.dolphin-emu$'; then
  flatpak run org.DolphinEmu.dolphin-emu --version || true
else
  echo "Dolphin not found after install"
  exit 1
fi

echo "[6/6] Done"
if command -v dolphin-emu >/dev/null 2>&1; then
  echo "Launch Dolphin with: dolphin-emu"
else
  echo "Launch Dolphin with: flatpak run org.DolphinEmu.dolphin-emu"
fi
echo "Optional graphics deps (if needed): sudo apt install -y mesa-vulkan-drivers libvulkan1"
