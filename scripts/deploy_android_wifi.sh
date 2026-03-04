#!/usr/bin/env bash

set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ADB_BIN="${ADB_BIN:-${ANDROID_SDK_ROOT:-$HOME/.android-sdk}/platform-tools/adb}"
TFM="${TFM:-net9.0-android}"
PROJECT_FILE="${PROJECT_FILE:-NutritionTracker.csproj}"

if [[ ! -x "$ADB_BIN" ]]; then
  if command -v adb >/dev/null 2>&1; then
    ADB_BIN="$(command -v adb)"
  else
    echo "❌ adb introuvable. Installe Android platform-tools ou définis ADB_BIN." >&2
    exit 1
  fi
fi

usage() {
  cat <<EOF
Usage:
  ./scripts/deploy_android_wifi.sh pair <ip:pair_port> [pairing_code]
  ./scripts/deploy_android_wifi.sh connect <ip:adb_port>
  ./scripts/deploy_android_wifi.sh install [device_serial]
  ./scripts/deploy_android_wifi.sh status
  ./scripts/deploy_android_wifi.sh all <ip:pair_port> <ip:adb_port> [pairing_code]

Examples:
  ./scripts/deploy_android_wifi.sh pair 192.168.1.50:42081 123456
  ./scripts/deploy_android_wifi.sh connect 192.168.1.50:45553
  ./scripts/deploy_android_wifi.sh install 192.168.1.50:45553
  ./scripts/deploy_android_wifi.sh all 192.168.1.50:42081 192.168.1.50:45553 123456

Phone prerequisites (Android 11+):
  1) Options développeur > Débogage sans fil (Wireless debugging): ON
  2) "Associer l'appareil avec le code" pour obtenir ip:port + code
EOF
}

require_dotnet() {
  if ! command -v dotnet >/dev/null 2>&1; then
    echo "❌ dotnet introuvable dans le PATH." >&2
    exit 1
  fi
}

cmd="${1:-}"
case "$cmd" in
  pair)
    endpoint="${2:-}"
    pairing_code="${3:-}"
    if [[ -z "$endpoint" ]]; then
      usage
      exit 1
    fi

    if [[ -n "$pairing_code" ]]; then
      "$ADB_BIN" pair "$endpoint" "$pairing_code"
    else
      "$ADB_BIN" pair "$endpoint"
    fi
    ;;

  connect)
    endpoint="${2:-}"
    if [[ -z "$endpoint" ]]; then
      usage
      exit 1
    fi

    "$ADB_BIN" connect "$endpoint"
    "$ADB_BIN" devices -l
    ;;

  status)
    "$ADB_BIN" devices -l
    ;;

  install)
    require_dotnet
    serial="${2:-}"

    pushd "$PROJECT_ROOT" >/dev/null

    if [[ -n "$serial" ]]; then
      echo "📲 Déploiement sur $serial"
      dotnet build "$PROJECT_FILE" -t:Install -f "$TFM" -p:AndroidDeviceSerial="$serial" -v minimal
    else
      echo "📲 Déploiement sur l'unique appareil connecté"
      dotnet build "$PROJECT_FILE" -t:Install -f "$TFM" -v minimal
    fi

    popd >/dev/null
    ;;

  all)
    pair_endpoint="${2:-}"
    connect_endpoint="${3:-}"
    pairing_code="${4:-}"

    if [[ -z "$pair_endpoint" || -z "$connect_endpoint" ]]; then
      usage
      exit 1
    fi

    if [[ -n "$pairing_code" ]]; then
      "$ADB_BIN" pair "$pair_endpoint" "$pairing_code"
    else
      "$ADB_BIN" pair "$pair_endpoint"
    fi

    "$ADB_BIN" connect "$connect_endpoint"
    "$ADB_BIN" devices -l

    require_dotnet
    pushd "$PROJECT_ROOT" >/dev/null
    dotnet build "$PROJECT_FILE" -t:Install -f "$TFM" -p:AndroidDeviceSerial="$connect_endpoint" -v minimal
    popd >/dev/null
    ;;

  *)
    usage
    exit 1
    ;;
esac
