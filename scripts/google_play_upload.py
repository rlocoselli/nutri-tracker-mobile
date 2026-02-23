#!/usr/bin/env python3
import argparse
import datetime as dt
from pathlib import Path

from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.errors import HttpError
from googleapiclient.http import MediaFileUpload


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Upload Android AAB to Google Play Console track")
    parser.add_argument("--service-account", required=True, help="Path to service account JSON")
    parser.add_argument("--package-name", required=True, help="Android package name")
    parser.add_argument("--track", default="internal", help="Play track: internal, alpha, beta, production")
    parser.add_argument("--release-status", default="completed", choices=["draft", "completed", "halted", "inProgress"], help="Release status")
    parser.add_argument("--rollout-percentage", default="100", help="Staged rollout percentage 1-100. <100 sets inProgress on supported tracks")
    parser.add_argument("--aab", required=True, help="Path to .aab file")
    parser.add_argument("--release-name", default="", help="Optional release name")
    parser.add_argument("--default-language", default="en-US", help="Play listing language code")
    parser.add_argument("--listing-title", default="", help="Store listing title")
    parser.add_argument("--short-description", default="", help="Store listing short description")
    parser.add_argument("--full-description", default="", help="Store listing full description")
    parser.add_argument("--privacy-policy-url", default="", help="Privacy policy URL (manual setup in Play Console)")
    parser.add_argument("--additional-languages", default="", help="Comma-separated extra listing languages (e.g. fr-FR,es-ES)")
    parser.add_argument("--localizations-dir", default="", help="Base dir containing per-language listing/image files")
    parser.add_argument("--first-deploy", action="store_true", help="Force first-deploy validation rules")
    parser.add_argument("--ack-manual-compliance", action="store_true", help="Acknowledge manual tasks (content rating, age, data safety, app access)")
    parser.add_argument("--icon", default="", help="Path to store icon image")
    parser.add_argument("--feature-graphic", default="", help="Path to feature graphic image")
    parser.add_argument("--phone-screenshots-dir", default="", help="Directory containing phone screenshot images")
    return parser.parse_args()


def has_existing_release(service, package_name: str, edit_id: str) -> bool:
    tracks = service.edits().tracks().list(packageName=package_name, editId=edit_id).execute()
    for track in tracks.get("tracks", []):
        if track.get("releases"):
            return True
    return False


def ensure_first_deploy_requirements(args: argparse.Namespace, detected_first_deploy: bool) -> None:
    if not (args.first_deploy or detected_first_deploy):
        return

    missing = []
    if not args.listing_title.strip():
        missing.append("--listing-title")
    if not args.short_description.strip():
        missing.append("--short-description")
    if not args.full_description.strip():
        missing.append("--full-description")
    if not args.privacy_policy_url.strip():
        missing.append("--privacy-policy-url")

    if missing:
        raise SystemExit(
            "[ERROR] First deploy detected: missing listing metadata: " + ", ".join(missing)
        )

    if not args.ack_manual_compliance:
        raise SystemExit(
            "[ERROR] First deploy requires manual Google Play compliance steps. "
            "Re-run with --ack-manual-compliance after completing: content rating, target age, data safety, app access, ads declaration."
        )

    print(
        "[INFO] Privacy policy URL for first deploy: "
        f"{args.privacy_policy_url.strip()}\n"
        "       Set it in Play Console > App content > Privacy policy (manual step)."
    )


def update_listing(service, package_name: str, edit_id: str, args: argparse.Namespace) -> None:
    if not any([args.listing_title.strip(), args.short_description.strip(), args.full_description.strip()]):
        print(f"[WARN] Skipping listing metadata update ({args.default_language}): no title/short/full description provided")
        return

    body = {}
    if args.listing_title.strip():
        body["title"] = args.listing_title.strip()
    if args.short_description.strip():
        body["shortDescription"] = args.short_description.strip()
    if args.full_description.strip():
        body["fullDescription"] = args.full_description.strip()

    service.edits().listings().update(
        packageName=package_name,
        editId=edit_id,
        language=args.default_language,
        body=body,
    ).execute()
    print(f"[INFO] Updated listing metadata ({args.default_language})")


def update_listing_for_language(
    service,
    package_name: str,
    edit_id: str,
    language: str,
    title: str,
    short_description: str,
    full_description: str,
) -> None:
    body = {}
    if title.strip():
        body["title"] = title.strip()
    if short_description.strip():
        body["shortDescription"] = short_description.strip()
    if full_description.strip():
        body["fullDescription"] = full_description.strip()

    if not body:
        return

    service.edits().listings().update(
        packageName=package_name,
        editId=edit_id,
        language=language,
        body=body,
    ).execute()
    print(f"[INFO] Updated listing metadata ({language})")


def load_localized_listing(base_dir: Path, language: str) -> tuple[str, str, str]:
    lang_dir = base_dir / language
    title_path = lang_dir / "listing-title.txt"
    short_path = lang_dir / "short-description.txt"
    full_path = lang_dir / "full-description.txt"

    title = title_path.read_text(encoding="utf-8").strip() if title_path.exists() else ""
    short_description = short_path.read_text(encoding="utf-8").strip() if short_path.exists() else ""
    full_description = full_path.read_text(encoding="utf-8").strip() if full_path.exists() else ""
    if not any([title, short_description, full_description]):
        print(f"[WARN] No localized listing text found for {language} in {lang_dir}")
    return title, short_description, full_description


def upload_images(service, package_name: str, edit_id: str, language: str, image_type: str, image_paths: list[Path]) -> None:
    if not image_paths:
        return

    service.edits().images().deleteall(
        packageName=package_name,
        editId=edit_id,
        language=language,
        imageType=image_type,
    ).execute()

    for image_path in image_paths:
        if not image_path.exists() or not image_path.is_file():
            continue

        mime = "image/png"
        if image_path.suffix.lower() in {".jpg", ".jpeg"}:
            mime = "image/jpeg"

        service.edits().images().upload(
            packageName=package_name,
            editId=edit_id,
            language=language,
            imageType=image_type,
            media_body=MediaFileUpload(str(image_path), mimetype=mime),
        ).execute()

    print(f"[INFO] Uploaded {len(image_paths)} image(s) for {image_type} ({language})")


def update_graphics(service, package_name: str, edit_id: str, args: argparse.Namespace) -> None:
    icon_paths: list[Path] = [Path(args.icon)] if args.icon.strip() else []
    feature_paths: list[Path] = [Path(args.feature_graphic)] if args.feature_graphic.strip() else []

    screenshot_paths: list[Path] = []
    if args.phone_screenshots_dir.strip():
        screenshot_paths = sorted(
            p for p in Path(args.phone_screenshots_dir).glob("*")
            if p.suffix.lower() in {".png", ".jpg", ".jpeg", ".webp"}
        )

    upload_images(service, args.package_name, edit_id, args.default_language, "icon", icon_paths)
    upload_images(service, args.package_name, edit_id, args.default_language, "featureGraphic", feature_paths)
    upload_images(service, args.package_name, edit_id, args.default_language, "phoneScreenshots", screenshot_paths)


def update_graphics_for_language(service, package_name: str, edit_id: str, language: str, language_dir: Path) -> None:
    icon_paths: list[Path] = [language_dir / "icon.png"] if (language_dir / "icon.png").exists() else []
    feature_paths: list[Path] = [language_dir / "feature-graphic.png"] if (language_dir / "feature-graphic.png").exists() else []

    screenshots_dir = language_dir / "phone-screenshots"
    screenshot_paths: list[Path] = []
    if screenshots_dir.exists() and screenshots_dir.is_dir():
        screenshot_paths = sorted(
            p for p in screenshots_dir.glob("*") if p.suffix.lower() in {".png", ".jpg", ".jpeg", ".webp"}
        )

    upload_images(service, package_name, edit_id, language, "icon", icon_paths)
    upload_images(service, package_name, edit_id, language, "featureGraphic", feature_paths)
    upload_images(service, package_name, edit_id, language, "phoneScreenshots", screenshot_paths)


def parse_languages(raw: str) -> list[str]:
    if not raw.strip():
        return []
    parts = [item.strip() for item in raw.split(",")]
    return [item for item in parts if item]


def main() -> None:
    args = parse_args()

    try:
        rollout_percentage = float(args.rollout_percentage)
    except ValueError as exc:
        raise SystemExit("[ERROR] --rollout-percentage must be a number between 1 and 100") from exc

    if rollout_percentage <= 0 or rollout_percentage > 100:
        raise SystemExit("[ERROR] --rollout-percentage must be between 1 and 100")

    resolved_status = args.release_status
    user_fraction = None
    additional_languages = [
        lang for lang in parse_languages(args.additional_languages) if lang != args.default_language
    ]
    if args.track != "internal" and rollout_percentage < 100:
        resolved_status = "inProgress"
        user_fraction = round(rollout_percentage / 100.0, 4)
    elif args.track == "internal" and rollout_percentage < 100:
        print("[WARN] rollout percentage < 100 ignored for internal track")

    try:
        scopes = ["https://www.googleapis.com/auth/androidpublisher"]
        creds = service_account.Credentials.from_service_account_file(args.service_account, scopes=scopes)
        service = build("androidpublisher", "v3", credentials=creds, cache_discovery=False)

        edit = service.edits().insert(packageName=args.package_name, body={}).execute()
        edit_id = edit["id"]
        print(f"[INFO] Created edit: {edit_id}")

        detected_first_deploy = not has_existing_release(service, args.package_name, edit_id)
        ensure_first_deploy_requirements(args, detected_first_deploy)
        if detected_first_deploy and resolved_status != "draft":
            print(
                "[WARN] First deploy detected on a draft app. "
                "Forcing release status to 'draft' to satisfy Google Play constraints."
            )
            resolved_status = "draft"
            user_fraction = None

        bundle = service.edits().bundles().upload(
            packageName=args.package_name,
            editId=edit_id,
            media_body=MediaFileUpload(args.aab, mimetype="application/octet-stream"),
        ).execute()

        version_code = str(bundle["versionCode"])
        print(f"[INFO] Uploaded bundle versionCode={version_code}")

        release_name = args.release_name or f"Automated release {dt.datetime.utcnow():%Y-%m-%d %H:%M UTC}"
        release_body = {
            "name": release_name,
            "versionCodes": [version_code],
            "status": resolved_status,
        }
        if user_fraction is not None:
            release_body["userFraction"] = user_fraction

        track_body = {"releases": [release_body]}

        service.edits().tracks().update(
            packageName=args.package_name,
            editId=edit_id,
            track=args.track,
            body=track_body,
        ).execute()
        if user_fraction is not None:
            print(f"[INFO] Updated track '{args.track}' with staged rollout {rollout_percentage:.2f}%")
        else:
            print(f"[INFO] Updated track '{args.track}'")

        update_listing(service, args.package_name, edit_id, args)
        update_graphics(service, args.package_name, edit_id, args)

        if args.localizations_dir.strip() and additional_languages:
            localizations_dir = Path(args.localizations_dir)
            for language in additional_languages:
                title, short_description, full_description = load_localized_listing(localizations_dir, language)
                update_listing_for_language(
                    service,
                    args.package_name,
                    edit_id,
                    language,
                    title,
                    short_description,
                    full_description,
                )
                update_graphics_for_language(
                    service,
                    args.package_name,
                    edit_id,
                    language,
                    localizations_dir / language,
                )

        try:
            service.edits().commit(packageName=args.package_name, editId=edit_id).execute()
            print("[OK] Edit committed successfully")
        except HttpError as commit_err:
            commit_status = getattr(commit_err.resp, "status", None)
            commit_body = str(commit_err)
            draft_app_error = "Only releases with status draft may be created on draft app"

            if commit_status == 400 and draft_app_error in commit_body and resolved_status != "draft":
                print(
                    "[WARN] Commit rejected because app is still draft. "
                    "Retrying with release status 'draft'."
                )
                release_body["status"] = "draft"
                release_body.pop("userFraction", None)
                service.edits().tracks().update(
                    packageName=args.package_name,
                    editId=edit_id,
                    track=args.track,
                    body={"releases": [release_body]},
                ).execute()
                service.edits().commit(packageName=args.package_name, editId=edit_id).execute()
                print("[OK] Edit committed successfully (draft-app fallback)")
            else:
                raise

    except HttpError as err:
        status = getattr(err.resp, "status", None)
        body = str(err)

        if status == 404 and ("Package not found" in body or "edits" in body):
            raise SystemExit(
                "[ERROR] Google Play package not found for this account/project.\n"
                f"Package requested: {args.package_name}\n"
                "Fix:\n"
                "  1) In Play Console, create/import the app with EXACT same package name.\n"
                "  2) Ensure the service account has access to that app in Play Console (Users and permissions).\n"
                "  3) Verify workflow variable GOOGLE_PLAY_PACKAGE_NAME matches csproj ApplicationId.\n"
                f"Original error: {body}"
            )

        if status == 403 and ("SERVICE_DISABLED" in body or "Android Developer API" in body or "androidpublisher.googleapis.com" in body):
            raise SystemExit(
                "[ERROR] Google Play Android Developer API is disabled or not yet propagated for the GCP project used by this service account.\n"
                "Fix:\n"
                "  1) Open Google Cloud Console for the SAME project as the service-account JSON.\n"
                "  2) Enable API: https://console.developers.google.com/apis/api/androidpublisher.googleapis.com/overview\n"
                "  3) Wait 2-10 minutes for propagation.\n"
                "  4) In Play Console, ensure this service account is invited with release permissions for the app.\n"
                f"Original error: {body}"
            )

        raise SystemExit(f"[ERROR] Google Play API request failed ({status}): {body}")


if __name__ == "__main__":
    main()
