#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
from typing import Tuple

from PIL import Image, ImageDraw, ImageFont


def load_font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for candidate in ["DejaVuSans-Bold.ttf", "Arial.ttf", "LiberationSans-Bold.ttf"]:
        try:
            return ImageFont.truetype(candidate, size)
        except Exception:
            continue
    return ImageFont.load_default()


def draw_centered_text(draw: ImageDraw.ImageDraw, text: str, box: Tuple[int, int, int, int], font, fill: str) -> None:
    x1, y1, x2, y2 = box
    bbox = draw.textbbox((0, 0), text, font=font)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    tx = x1 + (x2 - x1 - tw) // 2
    ty = y1 + (y2 - y1 - th) // 2
    draw.text((tx, ty), text, font=font, fill=fill)


def make_icon(path: Path) -> None:
    size = 512
    img = Image.new("RGB", (size, size), "#16B39A")
    draw = ImageDraw.Draw(img)
    draw.rounded_rectangle((20, 20, size - 20, size - 20), radius=110, fill="#0F8F7C")
    draw.arc((110, 200, 400, 440), start=18, end=155, fill="white", width=22)
    draw.polygon([(330, 140), (275, 155), (240, 190), (220, 225), (258, 216), (285, 219), (270, 248), (238, 292), (292, 276), (332, 228), (360, 150)], fill="white")
    draw_centered_text(draw, "NT", (0, 315, size, 500), load_font(90), "white")
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, format="PNG")


def make_feature_graphic(path: Path, title: str, subtitle: str) -> None:
    w, h = 1024, 500
    img = Image.new("RGB", (w, h), "#16B39A")
    draw = ImageDraw.Draw(img)
    draw.rounded_rectangle((32, 32, w - 32, h - 32), radius=70, fill="#0F8F7C")

    draw.ellipse((650, -40, 1100, 420), fill="#129885")
    draw.ellipse((730, 120, 1060, 460), fill="#18A890")

    title_font = load_font(72)
    subtitle_font = load_font(34)
    draw.text((70, 150), title, font=title_font, fill="white")
    draw.text((70, 245), subtitle, font=subtitle_font, fill="#E8FFF9")

    draw.polygon([(840, 170), (790, 182), (760, 210), (744, 240), (775, 234), (796, 236), (783, 260), (756, 300), (802, 286), (842, 244), (868, 176)], fill="white")
    draw.arc((690, 225, 930, 430), start=22, end=150, fill="white", width=18)

    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, format="PNG")


def make_phone_screenshot(path: Path, index: int, title: str, line1: str, line2: str) -> None:
    w, h = 1080, 1920
    img = Image.new("RGB", (w, h), "#F5F7FB")
    draw = ImageDraw.Draw(img)

    draw.rectangle((0, 0, w, 260), fill="#0F8F7C")
    draw.text((56, 84), title, font=load_font(58), fill="white")

    card_top = 330
    for i in range(3):
        y = card_top + i * 340
        draw.rounded_rectangle((56, y, w - 56, y + 280), radius=34, fill="white", outline="#DDE3EC", width=2)
        draw.text((92, y + 58), f"{line1} {index}.{i+1}", font=load_font(40), fill="#1B2733")
        draw.text((92, y + 130), line2, font=load_font(30), fill="#5A6A7A")
        draw.rounded_rectangle((w - 310, y + 80, w - 92, y + 170), radius=20, fill="#16B39A")
        draw.text((w - 280, y + 102), "Voir", font=load_font(30), fill="white")

    footer = f"NutritionTracker • Capture {index}"
    draw.text((56, h - 90), footer, font=load_font(30), fill="#5A6A7A")

    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, format="PNG")


def make_marketing_screenshot(path: Path, index: int, app_title: str, headline: str, subline: str, cta: str) -> None:
    w, h = 1080, 1920
    img = Image.new("RGB", (w, h), "#0D1722")
    draw = ImageDraw.Draw(img)

    # gradient-like blocks
    draw.rectangle((0, 0, w, 600), fill="#0F8F7C")
    draw.ellipse((620, -120, 1180, 420), fill="#16B39A")
    draw.ellipse((700, 120, 1180, 620), fill="#22C4A8")

    # header
    draw.text((56, 86), app_title, font=load_font(66), fill="white")
    draw.text((56, 200), headline, font=load_font(58), fill="white")
    draw.text((56, 288), subline, font=load_font(34), fill="#DDFCF5")

    # phone mockup card
    card_x1, card_y1, card_x2, card_y2 = 72, 640, w - 72, h - 130
    draw.rounded_rectangle((card_x1, card_y1, card_x2, card_y2), radius=48, fill="#F5F7FB")

    # inner sections
    section_y = card_y1 + 56
    for i in range(3):
        y = section_y + i * 265
        draw.rounded_rectangle((card_x1 + 36, y, card_x2 - 36, y + 220), radius=28, fill="white", outline="#DDE3EC", width=2)
        draw.text((card_x1 + 66, y + 42), f"{headline[:24]} {i+1}", font=load_font(34), fill="#1B2733")
        draw.text((card_x1 + 66, y + 96), subline[:40], font=load_font(25), fill="#5A6A7A")
        draw.rounded_rectangle((card_x2 - 270, y + 66, card_x2 - 68, y + 148), radius=18, fill="#16B39A")
        draw.text((card_x2 - 232, y + 90), cta, font=load_font(28), fill="white")

    draw.text((56, h - 70), f"{app_title} • {index:02d}", font=load_font(28), fill="#8AA0B2")

    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, format="PNG")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate Google Play visual assets")
    parser.add_argument("--out-dir", default="play_assets/generated", help="Output base directory")
    parser.add_argument("--lang", default="en-US", help="Language code for assets")
    parser.add_argument("--app-title", default="NutritionTracker", help="App title")
    parser.add_argument("--subtitle", default="", help="Feature graphic subtitle (optional, language-aware fallback if empty)")
    parser.add_argument("--style", default="marketing", choices=["marketing", "simple"], help="Screenshot style preset")
    return parser.parse_args()


def build_listing_copy(lang: str, app_title: str) -> tuple[str, str, str]:
    is_en = lang.lower().startswith("en")
    title = app_title
    if is_en:
        short_description = "Track calories, protein, carbs and daily activity."
        full_description = (
            "NutritionTracker helps you track meals (text/photo), nutrition goals, "
            "and daily activity with actionable insights and personalized recommendations."
        )
    else:
        short_description = "Suivi calories, protéines, glucides et activité."
        full_description = (
            "NutritionTracker vous aide à suivre vos repas (texte/photo), "
            "vos objectifs nutritionnels et votre activité quotidienne."
        )
    return title, short_description, full_description


def resolve_subtitle(lang: str, provided_subtitle: str) -> str:
    if provided_subtitle.strip():
        return provided_subtitle.strip()
    if lang.lower().startswith("en"):
        return "Nutrition and activity tracking"
    return "Suivi nutrition et activité"


def main() -> None:
    args = parse_args()
    base = Path(args.out_dir) / args.lang
    subtitle = resolve_subtitle(args.lang, args.subtitle)

    make_icon(base / "icon.png")
    make_feature_graphic(base / "feature-graphic.png", args.app_title, subtitle)

    is_en = args.lang.lower().startswith("en")
    if is_en:
        labels = [
            ("Track Your Nutrition", "Calories, protein and carbs in one view"),
            ("Smart Meal Diary", "Add, edit and remove entries easily"),
            ("AI Meal Analysis", "Text + photo based nutrition estimate"),
            ("Manual Macros + Activity", "Steps and exercise impact included"),
            ("Personalized Recommendations", "Actionable insights from your data"),
            ("Profile & Language", "Multilingual experience and sync status"),
        ]
        cta = "Open"
    else:
        labels = [
            ("Suivi nutrition complet", "Calories, protéines et glucides en un coup d'œil"),
            ("Journal intelligent", "Ajout, modification et suppression faciles"),
            ("Analyse IA des repas", "Estimation nutrition via texte + photo"),
            ("Macros et activité", "Impact des pas et exercices inclus"),
            ("Recommandations personnalisées", "Conseils actionnables basés sur vos données"),
            ("Profil & langue", "Expérience multilingue et statut de sync"),
        ]
        cta = "Voir"

    for idx, (line1, line2) in enumerate(labels, start=1):
        target = base / "phone-screenshots" / f"screenshot-{idx:02d}.png"
        if args.style == "marketing":
            make_marketing_screenshot(target, idx, args.app_title, line1, line2, cta)
        else:
            make_phone_screenshot(target, idx, args.app_title, line1, line2)

    listing_title, short_description, full_description = build_listing_copy(args.lang, args.app_title)
    (base / "listing-title.txt").write_text(listing_title + "\n", encoding="utf-8")
    (base / "short-description.txt").write_text(short_description + "\n", encoding="utf-8")
    (base / "full-description.txt").write_text(full_description + "\n", encoding="utf-8")

    print(f"GENERATED:{base}")


if __name__ == "__main__":
    main()
