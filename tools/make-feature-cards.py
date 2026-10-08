#!/usr/bin/env python3
"""
Composes the feature images used in the README from the screenshots in docs/.

    python3 tools/make-feature-cards.py

Each card places one or two real screenshots, cropped to the part that shows the
feature, on the same violet-to-blue ground as docs/banner.svg and docs/promo.gif,
so the whole README reads as one set. Run tools/make-screenshots.ps1 first if the
screenshots change; this script only reframes them.

Requires Pillow.
"""
import random
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

DOCS = Path(__file__).resolve().parent.parent / "docs"

# Banner palette
BASE_TOP = (26, 22, 51)       # #1a1633
BASE_BOTTOM = (17, 15, 37)    # #110f25
VIOLET = (139, 92, 246)       # #8b5cf6
BLUE = (79, 123, 255)         # #4f7bff
CYAN = (20, 184, 230)         # #14b8e6


def ground(w, h, seed):
    """Vertical base gradient, two soft glows and a sparse star field."""
    base = Image.linear_gradient("L").resize((w, h))
    top, bottom = Image.new("RGB", (w, h), BASE_TOP), Image.new("RGB", (w, h), BASE_BOTTOM)
    out = Image.composite(bottom, top, base)

    glow = Image.new("RGB", (w, h), (0, 0, 0))
    g = ImageDraw.Draw(glow)
    g.ellipse((-w * 0.25, -h * 0.55, w * 0.55, h * 0.75), fill=tuple(int(v * 0.42) for v in VIOLET))
    g.ellipse((w * 0.5, h * 0.25, w * 1.3, h * 1.5), fill=tuple(int(v * 0.36) for v in BLUE))
    glow = glow.filter(ImageFilter.GaussianBlur(radius=min(w, h) * 0.28))
    out = ImageChops.add(out, glow)

    d = ImageDraw.Draw(out)
    rnd = random.Random(seed)
    for _ in range(int(w * h / 9000)):
        x, y = rnd.random() * w, rnd.random() * h
        r = rnd.choice((0.6, 0.8, 1.0, 1.3))
        a = rnd.randint(60, 170)
        d.ellipse((x - r, y - r, x + r, y + r), fill=(a, a, min(255, a + 30)))
    return out


def framed(shot, radius):
    """Rounded corners, a hairline border and a soft shadow; returns (layer, mask, pad)."""
    w, h = shot.size
    pad = int(radius * 2.2)
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, w - 1, h - 1), radius=radius, fill=255)

    shadow = Image.new("RGBA", (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
    sm = Image.new("L", shadow.size, 0)
    ImageDraw.Draw(sm).rounded_rectangle((pad, pad + pad // 3, pad + w, pad + h + pad // 3), radius=radius, fill=150)
    sm = sm.filter(ImageFilter.GaussianBlur(pad * 0.45))
    shadow.putalpha(sm)

    layer = Image.new("RGBA", shadow.size, (0, 0, 0, 0))
    layer.alpha_composite(shadow)
    s = shot.convert("RGBA")
    s.putalpha(mask)
    layer.alpha_composite(s, (pad, pad))
    ImageDraw.Draw(layer).rounded_rectangle((pad, pad, pad + w - 1, pad + h - 1), radius=radius,
                                            outline=(255, 255, 255, 38), width=2)
    return layer, pad


def place(card, shot, box_w, box_h, cx, cy, radius=18):
    """Scale shot to fit box_w x box_h, frame it and centre it on (cx, cy)."""
    k = min(box_w / shot.width, box_h / shot.height)
    s = shot.resize((round(shot.width * k), round(shot.height * k)), Image.LANCZOS)
    layer, pad = framed(s, radius)
    card.alpha_composite(layer, (round(cx - s.width / 2 - pad), round(cy - s.height / 2 - pad)))
    return s.size


def card(w, h, seed):
    return ground(w, h, seed).convert("RGBA")


def save(img, name):
    path = DOCS / name
    img.convert("RGB").save(path, optimize=True)
    print(f"  {name}  {img.width}x{img.height}  {path.stat().st_size // 1024} KB")


def open_doc(name, box=None):
    im = Image.open(DOCS / name).convert("RGB")
    return im.crop(box) if box else im


def build():
    W, H = 1600, 800

    # Capture: the frozen selection with its magnifier, and the floating stack beside it
    c = card(W, H, 11)
    region = open_doc("region.png", (300, 128, 1680, 968))
    stack = open_doc("stack-hover.png", (0, 0, 330, 760))
    place(c, region, 1080, 650, 975, 400)
    place(c, stack, 290, 620, 235, 410)
    save(c, "feature-capture.png")

    # Edit: the editor with marks and the presentation backdrop on
    c = card(W, H, 23)
    place(c, open_doc("backdrop.png", (40, 28, 1750, 900)), 1420, 690, W / 2, H / 2 + 6)
    save(c, "feature-editor.png")

    # Record: an area being recorded, with the timer and the stop button
    c = card(W, H, 37)
    place(c, open_doc("recording.png", (84, 20, 950, 660)), 1240, 680, W / 2, H / 2 + 6)
    save(c, "feature-recording.png")


if __name__ == "__main__":
    build()
