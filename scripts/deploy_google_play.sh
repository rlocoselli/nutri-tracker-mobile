#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_PATH="${PROJECT_PATH:-$ROOT_DIR/NutritionTracker.csproj}"
ANDROID_TARGET_FRAMEWORK="${ANDROID_TARGET_FRAMEWORK:-net9.0-android}"
PACKAGE_NAME="${GOOGLE_PLAY_PACKAGE_NAME:-com.audela.nutritiontracker}"
TRACK="${GOOGLE_PLAY_TRACK:-internal}"
RELEASE_STATUS="${GOOGLE_PLAY_RELEASE_STATUS:-completed}"
ROLLOUT_PERCENTAGE="${GOOGLE_PLAY_ROLLOUT_PERCENTAGE:-100}"
AUTO_BUMP_VERSION="${GOOGLE_PLAY_AUTO_BUMP_VERSION:-true}"
APPLICATION_VERSION="${GOOGLE_PLAY_APPLICATION_VERSION:-}"
SERVICE_ACCOUNT_JSON="${GOOGLE_PLAY_SERVICE_ACCOUNT_JSON:-}"
AAB_PATH="${GOOGLE_PLAY_AAB_PATH:-}"
DRY_RUN="${GOOGLE_PLAY_DRY_RUN:-false}"
VALIDATE_ONLY="${GOOGLE_PLAY_VALIDATE_ONLY:-false}"
MIN_TARGET_SDK="${GOOGLE_PLAY_MIN_TARGET_SDK:-36}"
DEFAULT_LANGUAGE="${GOOGLE_PLAY_DEFAULT_LANGUAGE:-en-US}"
ADDITIONAL_LANGUAGES_RAW="${GOOGLE_PLAY_ADDITIONAL_LANGUAGES:-fr-FR}"
EFFECTIVE_ADDITIONAL_LANGUAGES="$ADDITIONAL_LANGUAGES_RAW"
LISTING_TITLE="${GOOGLE_PLAY_LISTING_TITLE:-}"
SHORT_DESCRIPTION="${GOOGLE_PLAY_SHORT_DESCRIPTION:-}"
FULL_DESCRIPTION="${GOOGLE_PLAY_FULL_DESCRIPTION:-}"
PRIVACY_POLICY_URL="${GOOGLE_PLAY_PRIVACY_POLICY_URL:-https://www.audeladedonnees.fr/legal/privacy}"
FIRST_DEPLOY="${GOOGLE_PLAY_FIRST_DEPLOY:-false}"
ACK_MANUAL_COMPLIANCE="${GOOGLE_PLAY_ACK_MANUAL_COMPLIANCE:-false}"
AUTO_GENERATE_PLAY_ASSETS="${GOOGLE_PLAY_AUTO_GENERATE_ASSETS:-true}"
PLAY_ASSETS_DIR="${GOOGLE_PLAY_ASSETS_DIR:-$ROOT_DIR/play_assets/generated}"
PLAY_ASSETS_APP_TITLE="${GOOGLE_PLAY_ASSETS_APP_TITLE:-NutritionTracker}"
PLAY_ASSETS_SUBTITLE="${GOOGLE_PLAY_ASSETS_SUBTITLE:-Nutrition and activity tracking}"
PLAY_ASSETS_STYLE="${GOOGLE_PLAY_ASSETS_STYLE:-marketing}"

tfm_major_version() {
  local tfm="$1"
  if [[ "$tfm" =~ ^net([0-9]+)\. ]]; then
    echo "${BASH_REMATCH[1]}"
    return
  fi
  echo "0"
}

verify_target_sdk_guard() {
  local artifact_path="$1"
  local min_required="$2"

  if ! [[ "$min_required" =~ ^[0-9]+$ ]]; then
    echo "[ERROR] GOOGLE_PLAY_MIN_TARGET_SDK must be an integer (got '$min_required')."
    exit 1
  fi

  if [[ "$artifact_path" == *.apk ]]; then
    if command -v apkanalyzer >/dev/null 2>&1; then
      local target_sdk
      target_sdk="$(apkanalyzer manifest target-sdk "$artifact_path" 2>/dev/null || true)"
      target_sdk="$(printf '%s' "$target_sdk" | tr -d '\r\n')"
      if [[ -n "$target_sdk" && "$target_sdk" =~ ^[0-9]+$ ]]; then
        echo "[INFO] Artifact target SDK detected: $target_sdk"
        if (( target_sdk < min_required )); then
          echo "[ERROR] Artifact target SDK ($target_sdk) is lower than required minimum ($min_required)."
          exit 1
        fi
        return
      fi
    fi

    echo "[WARN] Could not read target SDK from APK with apkanalyzer; using framework guard fallback."
  fi

  local tfm_major
  tfm_major="$(tfm_major_version "$ANDROID_TARGET_FRAMEWORK")"
  if [[ "$tfm_major" =~ ^[0-9]+$ ]] && (( tfm_major > 0 )) && (( min_required >= 35 )) && (( tfm_major < 9 )); then
    echo "[ERROR] Target framework '$ANDROID_TARGET_FRAMEWORK' cannot satisfy target SDK >= $min_required for Google Play."
    echo "        Use net9.0-android (or newer) for compliant bundles."
    exit 1
  fi

  echo "[INFO] Target SDK guard passed (framework=$ANDROID_TARGET_FRAMEWORK, min_required=$min_required)."
}

if [[ -z "$APPLICATION_VERSION" && "$AUTO_BUMP_VERSION" == "true" ]]; then
  # Epoch seconds => monotonic numeric Android versionCode in CI.
  APPLICATION_VERSION="$(date +%s)"
fi

if [[ -n "$APPLICATION_VERSION" ]]; then
  if ! [[ "$APPLICATION_VERSION" =~ ^[0-9]+$ ]]; then
    echo "[ERROR] GOOGLE_PLAY_APPLICATION_VERSION must be a positive integer (versionCode)."
    exit 1
  fi
  if (( APPLICATION_VERSION <= 0 )); then
    echo "[ERROR] GOOGLE_PLAY_APPLICATION_VERSION must be > 0."
    exit 1
  fi
fi

if [[ "$VALIDATE_ONLY" == "true" ]]; then
  missing_vars=()
  missing_files=()

  if [[ -z "$SERVICE_ACCOUNT_JSON" ]]; then
    missing_vars+=("GOOGLE_PLAY_SERVICE_ACCOUNT_JSON")
  elif [[ ! -f "$SERVICE_ACCOUNT_JSON" ]]; then
    missing_files+=("GOOGLE_PLAY_SERVICE_ACCOUNT_JSON=$SERVICE_ACCOUNT_JSON")
  fi

  if [[ -z "$AAB_PATH" ]]; then
    [[ -z "${ANDROID_SIGNING_KEYSTORE_PATH:-}" ]] && missing_vars+=("ANDROID_SIGNING_KEYSTORE_PATH")
    [[ -z "${ANDROID_SIGNING_STORE_PASS:-}" ]] && missing_vars+=("ANDROID_SIGNING_STORE_PASS")
    [[ -z "${ANDROID_SIGNING_KEY_ALIAS:-}" ]] && missing_vars+=("ANDROID_SIGNING_KEY_ALIAS")
    [[ -z "${ANDROID_SIGNING_KEY_PASS:-}" ]] && missing_vars+=("ANDROID_SIGNING_KEY_PASS")

    if [[ -n "${ANDROID_SIGNING_KEYSTORE_PATH:-}" && ! -f "${ANDROID_SIGNING_KEYSTORE_PATH}" ]]; then
      missing_files+=("ANDROID_SIGNING_KEYSTORE_PATH=${ANDROID_SIGNING_KEYSTORE_PATH}")
    fi
  else
    if [[ ! -f "$AAB_PATH" ]]; then
      missing_files+=("GOOGLE_PLAY_AAB_PATH=$AAB_PATH")
    fi
  fi

  if [[ "$FIRST_DEPLOY" == "true" ]]; then
    [[ -z "$LISTING_TITLE" ]] && missing_vars+=("GOOGLE_PLAY_LISTING_TITLE")
    [[ -z "$SHORT_DESCRIPTION" ]] && missing_vars+=("GOOGLE_PLAY_SHORT_DESCRIPTION")
    [[ -z "$FULL_DESCRIPTION" ]] && missing_vars+=("GOOGLE_PLAY_FULL_DESCRIPTION")
    [[ -z "$PRIVACY_POLICY_URL" ]] && missing_vars+=("GOOGLE_PLAY_PRIVACY_POLICY_URL")
  fi

  echo "[VALIDATE] Google Play deploy preflight"
  echo "           Package: $PACKAGE_NAME"
  echo "           Track:   $TRACK"
  echo "           First:   $FIRST_DEPLOY"

  if (( ${#missing_vars[@]} > 0 )); then
    echo "[ERROR] Missing required variables:"
    for v in "${missing_vars[@]}"; do
      echo "  - $v"
    done
  fi

  if (( ${#missing_files[@]} > 0 )); then
    echo "[ERROR] Missing file paths:"
    for f in "${missing_files[@]}"; do
      echo "  - $f"
    done
  fi

  if (( ${#missing_vars[@]} > 0 || ${#missing_files[@]} > 0 )); then
    echo "[VALIDATE] FAILED"
    exit 2
  fi

  echo "[VALIDATE] OK - CI env appears ready for deploy"
  exit 0
fi

if [[ "$DRY_RUN" != "true" ]]; then
  if [[ -z "$SERVICE_ACCOUNT_JSON" ]]; then
    echo "[ERROR] GOOGLE_PLAY_SERVICE_ACCOUNT_JSON is required (path to service-account JSON)."
    exit 1
  fi

  if [[ ! -f "$SERVICE_ACCOUNT_JSON" ]]; then
    echo "[ERROR] Service account file not found: $SERVICE_ACCOUNT_JSON"
    exit 1
  fi
fi

if [[ "$DRY_RUN" != "true" ]]; then
  if [[ -z "$AAB_PATH" ]]; then
    : "${ANDROID_SIGNING_KEYSTORE_PATH:?ANDROID_SIGNING_KEYSTORE_PATH is required when GOOGLE_PLAY_AAB_PATH is not provided}"
    : "${ANDROID_SIGNING_STORE_PASS:?ANDROID_SIGNING_STORE_PASS is required when GOOGLE_PLAY_AAB_PATH is not provided}"
    : "${ANDROID_SIGNING_KEY_ALIAS:?ANDROID_SIGNING_KEY_ALIAS is required when GOOGLE_PLAY_AAB_PATH is not provided}"
    : "${ANDROID_SIGNING_KEY_PASS:?ANDROID_SIGNING_KEY_PASS is required when GOOGLE_PLAY_AAB_PATH is not provided}"

    if [[ ! -f "$ANDROID_SIGNING_KEYSTORE_PATH" ]]; then
      echo "[ERROR] Signing keystore not found: $ANDROID_SIGNING_KEYSTORE_PATH"
      exit 1
    fi

    if ! command -v keytool >/dev/null 2>&1; then
      echo "[ERROR] keytool not found in PATH. Install a JDK (Java 17 recommended in CI)."
      exit 1
    fi

    if ! keytool -list -keystore "$ANDROID_SIGNING_KEYSTORE_PATH" -storepass "$ANDROID_SIGNING_STORE_PASS" >/dev/null 2>&1; then
      echo "[ERROR] Cannot open keystore with provided ANDROID_SIGNING_STORE_PASS."
      exit 1
    fi

    KEYSTORE_TYPE="$(keytool -list -keystore "$ANDROID_SIGNING_KEYSTORE_PATH" -storepass "$ANDROID_SIGNING_STORE_PASS" 2>/dev/null | sed -n 's/^Keystore type: //p' | head -n1 | tr -d '\r\n')"
    SIGNING_KEY_PASS_EFFECTIVE="$ANDROID_SIGNING_KEY_PASS"
    if [[ "${KEYSTORE_TYPE^^}" == "PKCS12" ]]; then
      echo "[INFO] PKCS12 keystore detected. Using store password as effective key password for signing."
      SIGNING_KEY_PASS_EFFECTIVE="$ANDROID_SIGNING_STORE_PASS"
    fi

    if ! keytool -list -v -keystore "$ANDROID_SIGNING_KEYSTORE_PATH" -storepass "$ANDROID_SIGNING_STORE_PASS" -alias "$ANDROID_SIGNING_KEY_ALIAS" -keypass "$SIGNING_KEY_PASS_EFFECTIVE" >/dev/null 2>&1; then
      if [[ "$SIGNING_KEY_PASS_EFFECTIVE" != "$ANDROID_SIGNING_STORE_PASS" ]] && keytool -list -v -keystore "$ANDROID_SIGNING_KEYSTORE_PATH" -storepass "$ANDROID_SIGNING_STORE_PASS" -alias "$ANDROID_SIGNING_KEY_ALIAS" -keypass "$ANDROID_SIGNING_STORE_PASS" >/dev/null 2>&1; then
        echo "[WARN] Key password secret seems invalid for this keystore. Falling back to store password for signing."
        SIGNING_KEY_PASS_EFFECTIVE="$ANDROID_SIGNING_STORE_PASS"
      else
      echo "[ERROR] Cannot access alias '$ANDROID_SIGNING_KEY_ALIAS' with provided key password."
      echo "        Verify ANDROID_SIGNING_KEY_ALIAS and ANDROID_SIGNING_KEY_PASS secrets."
      exit 1
      fi
    fi

    echo "[INFO] Building signed AAB..."

    PUBLISH_ARGS=(
      -f "$ANDROID_TARGET_FRAMEWORK"
      -c Release
      /p:AndroidPackageFormat=aab
      /p:AndroidTargetSdkVersion=36
      /p:AndroidCompileSdkVersion=36
      /p:AndroidKeyStore=true
      /p:AndroidSigningKeyStore="$ANDROID_SIGNING_KEYSTORE_PATH"
      /p:AndroidSigningStorePass="$ANDROID_SIGNING_STORE_PASS"
      /p:AndroidSigningKeyAlias="$ANDROID_SIGNING_KEY_ALIAS"
      /p:AndroidSigningKeyPass="$SIGNING_KEY_PASS_EFFECTIVE"
    )

    if [[ -n "$APPLICATION_VERSION" ]]; then
      PUBLISH_ARGS+=(/p:ApplicationVersion="$APPLICATION_VERSION")
    fi

    dotnet publish "$PROJECT_PATH" "${PUBLISH_ARGS[@]}"

    ASSETS_FILE="$ROOT_DIR/obj/project.assets.json"
    if [[ -f "$ASSETS_FILE" ]]; then
      set +e
      SQLITE_GUARD_OUT="$(python3 - "$ASSETS_FILE" <<'PY'
import json
import sys

assets_path = sys.argv[1]
tracked = {
    "sqlitepclraw.bundle_green",
    "sqlitepclraw.lib.e_sqlite3.android",
    "sqlitepclraw.provider.e_sqlite3",
}

with open(assets_path, "r", encoding="utf-8") as f:
    data = json.load(f)

resolved = {}
for key in data.get("libraries", {}).keys():
    if "/" not in key:
        continue
    package_name, version = key.split("/", 1)
    name_l = package_name.lower()
    if name_l in tracked:
        resolved[name_l] = version

for name in sorted(tracked):
    version = resolved.get(name, "<not-resolved>")
    print(f"{name}={version}")

bad = [name for name, version in resolved.items() if version == "2.1.2"]
if bad:
    sys.exit(2)
PY
      )"
      SQLITE_GUARD_STATUS=$?
      set -e
      if [[ $SQLITE_GUARD_STATUS -ne 0 ]]; then
        echo "$SQLITE_GUARD_OUT"
        echo "[ERROR] Blocked: resolved SQLitePCLRaw version 2.1.2 (known 16 KB page-size issue in Play Console)."
        echo "        Ensure dependencies resolve to SQLitePCLRaw 2.1.11+ before deployment."
        exit 1
      fi
      echo "[INFO] SQLite resolution check"
      echo "$SQLITE_GUARD_OUT"
    fi

    PUBLISH_DIR="$ROOT_DIR/bin/Release/$ANDROID_TARGET_FRAMEWORK/publish"
    if [[ ! -d "$PUBLISH_DIR" ]]; then
      PUBLISH_DIR="$(find "$ROOT_DIR/bin/Release" -maxdepth 3 -type d -path "*/publish" | sort | tail -n 1)"
    fi

    if [[ -z "$PUBLISH_DIR" || ! -d "$PUBLISH_DIR" ]]; then
      echo "[ERROR] Publish directory not found under $ROOT_DIR/bin/Release"
      exit 1
    fi

    AAB_PATH="$(find "$PUBLISH_DIR" -maxdepth 1 -type f -name "*.aab" | head -n 1)"

    if [[ -z "$AAB_PATH" ]]; then
      echo "[ERROR] No AAB found in $PUBLISH_DIR"
      exit 1
    fi

    if ! command -v jarsigner >/dev/null 2>&1; then
      echo "[ERROR] jarsigner not found in PATH. Install a JDK (Java 17 recommended in CI)."
      exit 1
    fi

    echo "[INFO] Signing AAB with jarsigner..."
    jarsigner \
      -keystore "$ANDROID_SIGNING_KEYSTORE_PATH" \
      -storepass "$ANDROID_SIGNING_STORE_PASS" \
      -keypass "$SIGNING_KEY_PASS_EFFECTIVE" \
      -sigalg SHA256withRSA \
      -digestalg SHA-256 \
      "$AAB_PATH" \
      "$ANDROID_SIGNING_KEY_ALIAS" >/dev/null

    VERIFY_OUT="$(jarsigner -verify -verbose "$AAB_PATH" 2>&1 || true)"
    if echo "$VERIFY_OUT" | grep -qi "jar is unsigned"; then
      echo "[ERROR] AAB is still unsigned/invalid after jarsigner step: $AAB_PATH"
      exit 1
    fi

    if ! echo "$VERIFY_OUT" | grep -qi "jar verified"; then
      echo "[WARN] jarsigner verification did not print 'jar verified'. Output:"
      echo "$VERIFY_OUT"
    fi
  fi
fi

if [[ "$DRY_RUN" != "true" ]]; then
  if [[ ! -f "$AAB_PATH" ]]; then
    echo "[ERROR] AAB not found: $AAB_PATH"
    exit 1
  fi

  verify_target_sdk_guard "$AAB_PATH" "$MIN_TARGET_SDK"
fi

echo "[INFO] Uploading to Google Play..."
echo "       Package: $PACKAGE_NAME"
echo "       Track:   $TRACK"
echo "       Status:  $RELEASE_STATUS"
echo "       Rollout: $ROLLOUT_PERCENTAGE%"
echo "       VerCode: ${APPLICATION_VERSION:-<from csproj>}"
echo "       AAB:     $AAB_PATH"
echo "       Lang:    $DEFAULT_LANGUAGE"
echo "       First:   $FIRST_DEPLOY"
echo "       Policy:  $PRIVACY_POLICY_URL"
echo "       DryRun:  $DRY_RUN"
echo "       Validate:$VALIDATE_ONLY"

if [[ "$DEFAULT_LANGUAGE" != "en-US" ]]; then
  case ",$EFFECTIVE_ADDITIONAL_LANGUAGES," in
    *,en-US,*) ;;
    ",,") EFFECTIVE_ADDITIONAL_LANGUAGES="en-US" ;;
    *) EFFECTIVE_ADDITIONAL_LANGUAGES="$EFFECTIVE_ADDITIONAL_LANGUAGES,en-US" ;;
  esac
fi

echo "       Locales: default=$DEFAULT_LANGUAGE additional=${EFFECTIVE_ADDITIONAL_LANGUAGES:-<none>}"

if [[ "$AUTO_GENERATE_PLAY_ASSETS" == "true" && "$DRY_RUN" != "true" ]]; then
  echo "[INFO] Generating Play listing assets..."
  python3 "$ROOT_DIR/scripts/generate_play_assets.py" \
    --out-dir "$PLAY_ASSETS_DIR" \
    --lang "$DEFAULT_LANGUAGE" \
    --app-title "$PLAY_ASSETS_APP_TITLE" \
    --subtitle "$PLAY_ASSETS_SUBTITLE" \
    --style "$PLAY_ASSETS_STYLE"

  IFS=',' read -r -a ADDITIONAL_LANGUAGES_ARRAY <<< "$EFFECTIVE_ADDITIONAL_LANGUAGES"
  for lang in "${ADDITIONAL_LANGUAGES_ARRAY[@]}"; do
    lang="$(printf '%s' "$lang" | xargs)"
    if [[ -z "$lang" || "$lang" == "$DEFAULT_LANGUAGE" ]]; then
      continue
    fi
    python3 "$ROOT_DIR/scripts/generate_play_assets.py" \
      --out-dir "$PLAY_ASSETS_DIR" \
      --lang "$lang" \
      --app-title "$PLAY_ASSETS_APP_TITLE" \
      --style "$PLAY_ASSETS_STYLE"
  done
fi

LISTING_TITLE_PATH="$PLAY_ASSETS_DIR/$DEFAULT_LANGUAGE/listing-title.txt"
SHORT_DESCRIPTION_PATH="$PLAY_ASSETS_DIR/$DEFAULT_LANGUAGE/short-description.txt"
FULL_DESCRIPTION_PATH="$PLAY_ASSETS_DIR/$DEFAULT_LANGUAGE/full-description.txt"

if [[ -z "$LISTING_TITLE" && -f "$LISTING_TITLE_PATH" ]]; then
  LISTING_TITLE="$(<"$LISTING_TITLE_PATH")"
  echo "[INFO] Loaded generated listing title ($DEFAULT_LANGUAGE)"
fi
if [[ -z "$SHORT_DESCRIPTION" && -f "$SHORT_DESCRIPTION_PATH" ]]; then
  SHORT_DESCRIPTION="$(<"$SHORT_DESCRIPTION_PATH")"
  echo "[INFO] Loaded generated short description ($DEFAULT_LANGUAGE)"
fi
if [[ -z "$FULL_DESCRIPTION" && -f "$FULL_DESCRIPTION_PATH" ]]; then
  FULL_DESCRIPTION="$(<"$FULL_DESCRIPTION_PATH")"
  echo "[INFO] Loaded generated full description ($DEFAULT_LANGUAGE)"
fi

echo "[INFO] Listing payload check ($DEFAULT_LANGUAGE): title=${#LISTING_TITLE} short=${#SHORT_DESCRIPTION} full=${#FULL_DESCRIPTION}"

ICON_PATH="$PLAY_ASSETS_DIR/$DEFAULT_LANGUAGE/icon.png"
FEATURE_GRAPHIC_PATH="$PLAY_ASSETS_DIR/$DEFAULT_LANGUAGE/feature-graphic.png"
PHONE_SCREENSHOTS_DIR="$PLAY_ASSETS_DIR/$DEFAULT_LANGUAGE/phone-screenshots"

ARGS=(
  --service-account "$SERVICE_ACCOUNT_JSON"
  --package-name "$PACKAGE_NAME"
  --track "$TRACK"
  --release-status "$RELEASE_STATUS"
  --rollout-percentage "$ROLLOUT_PERCENTAGE"
  --aab "$AAB_PATH"
  --default-language "$DEFAULT_LANGUAGE"
  --localizations-dir "$PLAY_ASSETS_DIR"
)

if [[ -n "$EFFECTIVE_ADDITIONAL_LANGUAGES" ]]; then
  ARGS+=(--additional-languages "$EFFECTIVE_ADDITIONAL_LANGUAGES")
fi

if [[ -n "$LISTING_TITLE" ]]; then
  ARGS+=(--listing-title "$LISTING_TITLE")
fi
if [[ -n "$SHORT_DESCRIPTION" ]]; then
  ARGS+=(--short-description "$SHORT_DESCRIPTION")
fi
if [[ -n "$FULL_DESCRIPTION" ]]; then
  ARGS+=(--full-description "$FULL_DESCRIPTION")
fi
if [[ -n "$PRIVACY_POLICY_URL" ]]; then
  ARGS+=(--privacy-policy-url "$PRIVACY_POLICY_URL")
fi
if [[ "$FIRST_DEPLOY" == "true" ]]; then
  ARGS+=(--first-deploy)
fi
if [[ "$ACK_MANUAL_COMPLIANCE" == "true" ]]; then
  ARGS+=(--ack-manual-compliance)
fi
if [[ -f "$ICON_PATH" ]]; then
  ARGS+=(--icon "$ICON_PATH")
fi
if [[ -f "$FEATURE_GRAPHIC_PATH" ]]; then
  ARGS+=(--feature-graphic "$FEATURE_GRAPHIC_PATH")
fi
if [[ -d "$PHONE_SCREENSHOTS_DIR" ]]; then
  ARGS+=(--phone-screenshots-dir "$PHONE_SCREENSHOTS_DIR")
fi

if [[ "$DRY_RUN" == "true" ]]; then
  echo "[DRY RUN] Deployment command prepared successfully."
  echo "[DRY RUN] Python upload command:"
  printf 'python3 %q' "$ROOT_DIR/scripts/google_play_upload.py"
  for arg in "${ARGS[@]}"; do
    printf ' %q' "$arg"
  done
  printf '\n'
  echo "[DRY RUN] Skipped: signed AAB build, Google Play API upload, remote validation."
  exit 0
fi

python3 "$ROOT_DIR/scripts/google_play_upload.py" "${ARGS[@]}"

echo "[OK] Deployment finished."
