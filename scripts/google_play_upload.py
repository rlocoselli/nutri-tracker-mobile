#!/usr/bin/env python3
import argparse
import datetime as dt
from pathlib import Path

from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Upload Android AAB to Google Play Console track")
    parser.add_argument("--service-account", required=True, help="Path to service account JSON")
    parser.add_argument("--package-name", required=True, help="Android package name")
    parser.add_argument("--track", default="internal", help="Play track: internal, alpha, beta, production")
    parser.add_argument("--release-status", default="completed", choices=["draft", "completed", "halted"], help="Release status")
    parser.add_argument("--aab", required=True, help="Path to .aab file")
    parser.add_argument("--release-name", default="", help="Optional release name")
    parser.add_argument("--default-language", default="fr-FR", help="Play listing language code")
    parser.add_argument("--listing-title", default="", help="Store listing title")
    parser.add_argument("--short-description", default="", help="Store listing short description")
    parser.add_argument("--full-description", default="", help="Store listing full description")
    parser.add_argument("--privacy-policy-url", default="", help="Privacy policy URL (manual setup in Play Console)")
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


def main() -> None:
    args = parse_args()

    scopes = ["https://www.googleapis.com/auth/androidpublisher"]
    creds = service_account.Credentials.from_service_account_file(args.service_account, scopes=scopes)
    service = build("androidpublisher", "v3", credentials=creds, cache_discovery=False)

    edit = service.edits().insert(packageName=args.package_name, body={}).execute()
    edit_id = edit["id"]
    print(f"[INFO] Created edit: {edit_id}")

    detected_first_deploy = not has_existing_release(service, args.package_name, edit_id)
    ensure_first_deploy_requirements(args, detected_first_deploy)

    bundle = service.edits().bundles().upload(
        packageName=args.package_name,
        editId=edit_id,
        media_body=MediaFileUpload(args.aab, mimetype="application/octet-stream"),
    ).execute()

    version_code = str(bundle["versionCode"])
    print(f"[INFO] Uploaded bundle versionCode={version_code}")

    release_name = args.release_name or f"Automated release {dt.datetime.utcnow():%Y-%m-%d %H:%M UTC}"
    track_body = {
        "releases": [
            {
                "name": release_name,
                "versionCodes": [version_code],
                "status": args.release_status,
            }
        ]
    }

    service.edits().tracks().update(
        packageName=args.package_name,
        editId=edit_id,
        track=args.track,
        body=track_body,
    ).execute()
    print(f"[INFO] Updated track '{args.track}'")

    update_listing(service, args.package_name, edit_id, args)
    update_graphics(service, args.package_name, edit_id, args)

    service.edits().commit(packageName=args.package_name, editId=edit_id).execute()
    print("[OK] Edit committed successfully")


if __name__ == "__main__":
    main()
