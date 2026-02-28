from pathlib import Path

import imageio.v2 as imageio
import numpy as np
from PIL import Image, ImageDraw, ImageFont

WIDTH, HEIGHT = 1280, 720
FPS = 24
BG = (14, 23, 44)
ACCENT = (88, 166, 255)
TEXT = (240, 246, 255)
MUTED = (170, 184, 209)

SLIDES = [
    (
        "OAuth Verification Demo",
        "Nutrition Tracker",
        [
            "Video de verification pour scopes sensibles/restrictifs",
            "Mode YouTube: Non repertorie (Unlisted)",
            "Support: support@nutritiontracker.fr",
        ],
        7,
    ),
    (
        "1) Projet Google Cloud",
        "Montrer l'ecran OAuth consent",
        [
            "- Nom de l'application",
            "- Email support",
            "- Domaine autorise",
            "- Liens politique de confidentialite / conditions",
        ],
        10,
    ),
    (
        "2) Scopes demandes",
        "Expliquer pourquoi chaque scope est necessaire",
        [
            "- Afficher la liste des scopes",
            "- Associer chaque scope a une fonctionnalite",
            "- Mentionner minimisation des donnees",
        ],
        10,
    ),
    (
        "3) Flux de connexion",
        "Demonstration utilisateur",
        [
            "- Ouvrir l'app Nutrition Tracker",
            "- Login Google",
            "- Consentement",
            "- Retour reussi dans l'app",
        ],
        12,
    ),
    (
        "4) Utilisation reelle des scopes",
        "Montrer les ecrans fonctionnels",
        [
            "- Sync donnees nutrition / activite",
            "- Recommandations IA",
            "- Historique et objectifs",
        ],
        10,
    ),
    (
        "5) Donnees et suppression",
        "Conformite utilisateur",
        [
            "- Procedure suppression compte/donnees",
            "- Delais de retention",
            "- Contact DPO/support",
        ],
        9,
    ),
    (
        "6) Revocation d'acces",
        "Demonstration Google Account",
        [
            "- Security > Third-party access",
            "- Retirer Nutrition Tracker",
            "- Effet immediat dans l'app",
        ],
        8,
    ),
    (
        "Fin de la demonstration",
        "Merci",
        [
            "Soumettre ce lien YouTube dans OAuth verification",
            "Video unlisted, publique uniquement pour Google review",
        ],
        6,
    ),
]


def _font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for name in ["DejaVuSans.ttf", "Arial.ttf", "LiberationSans-Regular.ttf"]:
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def _make_frame(title: str, subtitle: str, bullets: list[str], t: float, duration: float) -> np.ndarray:
    img = Image.new("RGB", (WIDTH, HEIGHT), BG)
    draw = ImageDraw.Draw(img)

    progress = min(max(t / max(duration, 0.001), 0.0), 1.0)
    bar_w = int((WIDTH - 120) * progress)

    draw.rounded_rectangle((60, 44, WIDTH - 60, 96), radius=18, fill=(26, 38, 66))
    draw.rounded_rectangle((60, 44, 60 + bar_w, 96), radius=18, fill=ACCENT)

    title_font = _font(58)
    subtitle_font = _font(34)
    bullet_font = _font(32)

    draw.text((80, 140), title, fill=TEXT, font=title_font)
    draw.text((80, 220), subtitle, fill=MUTED, font=subtitle_font)

    y = 300
    for b in bullets:
        draw.text((100, y), f"• {b}", fill=TEXT, font=bullet_font)
        y += 66

    draw.text((80, HEIGHT - 58), "nutritiontracker.fr", fill=(121, 147, 196), font=_font(24))

    return np.array(img)


def main() -> None:
    out_dir = Path(__file__).resolve().parents[1] / "artifacts"
    out_dir.mkdir(parents=True, exist_ok=True)
    output = out_dir / "oauth-verification-demo.mp4"

    with imageio.get_writer(output, fps=FPS, codec="libx264", quality=8) as writer:
        for title, subtitle, bullets, seconds in SLIDES:
            frame_count = max(int(seconds * FPS), 1)
            for i in range(frame_count):
                frame = _make_frame(title, subtitle, bullets, i / FPS, seconds)
                writer.append_data(frame)

    print(output)


if __name__ == "__main__":
    main()
