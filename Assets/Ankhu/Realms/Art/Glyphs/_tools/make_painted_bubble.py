#!/usr/bin/env python3
"""Offline generator for the painted papyrus speech bubble (the "Painted" skin of RealmSpeechBubble).

Writes into ../Resources/RealmGlyphs:
  ui_papyrus.png        512x256  sheet of papyrus: two crossed fibre layers, aged edges, torn (deckle) outline.
                                 9 sliced at 72 px on every side.
  ui_papyrus_frame.png  512x256  the ruled border scribes drew round a vignette: an outer red ochre rule and an
                                 inner carbon black rule, both brushed (uneven weight, dry-brush breaks).
                                 Transparent centre, 9 sliced at 72 px.
  ui_papyrus_tail.png   128x128  the tail: a torn papyrus tongue, apex at bottom centre.
  ui_papyrus_tail_ink.png 128x128 its brushed black outline, drawn under the tail fill so only the edge shows.
  ui_ink_caret.png      16x64    a reed pen stroke used as the caret.

The colours are baked into the textures (the skin tints them white). Everything is deterministic (fixed seeds),
so running the script again reproduces the same files. Power of two sizes on both axes.

Usage: python3 make_painted_bubble.py   (needs Pillow and numpy)
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "Resources", "RealmGlyphs")

PAPYRUS = np.array([234, 204, 140], dtype=np.float32)       # fresh sheet, warm straw
PAPYRUS_DARK = np.array([168, 116, 58], dtype=np.float32)   # aged edge and fibre shadows
OCHRE = np.array([150, 46, 26], dtype=np.float32)           # red ochre rule (rubric red)
CARBON = np.array([32, 22, 14], dtype=np.float32)           # lamp black ink


def smooth_noise(shape, cells, rng, wrap_x=False):
    """Value noise: random grid resized bicubic. cells = (rows, cols)."""
    g = rng.random(cells).astype(np.float32)
    if wrap_x:
        g[:, -1] = g[:, 0]
    img = Image.fromarray((g * 255).astype(np.uint8)).resize((shape[1], shape[0]), Image.BICUBIC)
    return np.asarray(img, dtype=np.float32) / 255.0


def fibres(h, w, rng, horizontal=True, count=90, strength=0.10):
    """Long thin fibre strips: each strip has its own lightness and a slow variation along its length."""
    if not horizontal:
        return fibres(w, h, rng, True, count, strength).T
    field = np.zeros((h, w), dtype=np.float32)
    y = 0.0
    while y < h:
        thick = rng.uniform(1.2, 5.5)
        tone = rng.normal(0.0, 1.0)
        along = smooth_noise((1, w), (1, max(2, w // 48)), rng)[0] * 0.8 + 0.2
        y0, y1 = int(y), int(min(h, y + thick))
        if y1 > y0:
            field[y0:y1, :] += tone * along
        y += thick
    field = np.asarray(Image.fromarray(((field - field.min()) / (np.ptp(field) + 1e-6) * 255).astype(np.uint8))
                       .filter(ImageFilter.GaussianBlur(0.7)), dtype=np.float32) / 255.0
    return (field - 0.5) * 2.0 * strength


def edge_distance(h, w):
    yy, xx = np.mgrid[0:h, 0:w]
    return np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy)).astype(np.float32)


def rounded_distance(h, w, radius):
    """Distance inside a rounded rectangle (positive inside), in pixels."""
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    cx = np.clip(xx, radius, w - 1 - radius)
    cy = np.clip(yy, radius, h - 1 - radius)
    corner = radius - np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    straight = edge_distance(h, w)
    inside_corner = ((xx < radius) | (xx > w - 1 - radius)) & ((yy < radius) | (yy > h - 1 - radius))
    return np.where(inside_corner, corner, straight)


def save_rgba(name, rgb, alpha):
    rgba = np.zeros(rgb.shape[:2] + (4,), dtype=np.uint8)
    rgba[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    rgba[..., 3] = np.clip(alpha * 255.0, 0, 255).astype(np.uint8)
    Image.fromarray(rgba, "RGBA").save(os.path.join(OUT, name + ".png"), optimize=True)


def papyrus_sheet(h, w, rng, inset=10, radius=26):
    """Returns rgb (h,w,3) and alpha (h,w) for a torn papyrus sheet."""
    # Fibres: the front layer runs horizontally, the back layer (seen through) vertically and fainter.
    tone = fibres(h, w, rng, True, strength=0.16) + fibres(h, w, rng, False, strength=0.045)
    blotch = (smooth_noise((h, w), (5, 9), rng) - 0.5) * 0.10
    grain = (rng.random((h, w)).astype(np.float32) - 0.5) * 0.05
    # Torn outline: distance to a rounded rect, pushed in and out by two octaves of noise.
    d = rounded_distance(h, w, radius) - inset
    tear = (smooth_noise((h, w), (12, 24), rng) - 0.5) * 9.0 + (smooth_noise((h, w), (40, 80), rng) - 0.5) * 3.5
    d = d + tear
    alpha = np.clip(d / 1.6 + 0.5, 0, 1)
    # Age: darker towards the torn edge, a little darker in blotches.
    age = np.clip(1.0 - d / 30.0, 0, 1) ** 1.6
    k = np.clip(0.42 * age + 0.5 * np.clip(-blotch, 0, None), 0, 1)
    base = PAPYRUS[None, None, :] * (1 - k[..., None]) + PAPYRUS_DARK[None, None, :] * k[..., None]
    light = 1.0 + tone + blotch + grain
    rgb = base * light[..., None]
    # A thin darker lip right at the tear, as if the fibres were frayed.
    lip = np.exp(-((d - 1.5) / 1.6) ** 2)
    rgb = rgb * (1 - 0.25 * lip[..., None])
    return rgb, alpha


def brushed_rule(h, w, rng, inset, width, radius, color, dryness):
    """A ruled line drawn with a brush: weight wobbles along the stroke and dry patches break it."""
    d = rounded_distance(h, w, radius) - inset
    wobble = (smooth_noise((h, w), (6, 12), rng) - 0.5) * 1.4
    wgt = width * (0.8 + 0.45 * smooth_noise((h, w), (4, 10), rng))
    band = np.clip((wgt * 0.5 - np.abs(d + wobble)) / 0.9 + 0.5, 0, 1)
    dry = smooth_noise((h, w), (30, 60), rng)
    streak = smooth_noise((h, w), (64, 8), rng)
    coverage = np.clip(1.0 - dryness * np.clip(dry * 1.6 - 0.75, 0, 1) - 0.25 * dryness * streak, 0.15, 1)
    a = band * coverage
    rgb = np.broadcast_to(color, (h, w, 3)).copy()
    return rgb, a


def over(dst_rgb, dst_a, src_rgb, src_a):
    out_a = src_a + dst_a * (1 - src_a)
    safe = np.where(out_a > 1e-5, out_a, 1)
    out_rgb = (src_rgb * src_a[..., None] + dst_rgb * (dst_a * (1 - src_a))[..., None]) / safe[..., None]
    return out_rgb, out_a


def main():
    os.makedirs(OUT, exist_ok=True)
    rng = np.random.default_rng(2611)
    W, H = 512, 256

    rgb, a = papyrus_sheet(H, W, rng)
    save_rgba("ui_papyrus", rgb, a)

    # Frame: outer red rule, inner black rule, like the borders round a Book of the Dead vignette.
    r1, a1 = brushed_rule(H, W, rng, inset=22, width=5.0, radius=20, color=OCHRE, dryness=0.55)
    r2, a2 = brushed_rule(H, W, rng, inset=30, width=2.4, radius=14, color=CARBON, dryness=0.35)
    fr, fa = over(r1, a1, r2, a2)
    save_rgba("ui_papyrus_frame", fr, fa)

    # Tail: a torn papyrus tongue pointing down, plus its ink edge.
    n = 128
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    cx = (n - 1) / 2.0
    half = (n - 1) / 2.0 * (1.0 - yy / (n - 1.0)) ** 1.15   # half width shrinking to the apex
    half = half + (smooth_noise((n, n), (10, 10), rng) - 0.5) * 6.0
    dist = half - np.abs(xx - cx)                            # positive inside
    tail_a = np.clip(dist / 1.4 + 0.5, 0, 1) * np.clip((n - 3 - yy) / 2.0, 0, 1)
    sheet_rgb, _ = papyrus_sheet(n, n, rng, inset=-40, radius=4)
    save_rgba("ui_papyrus_tail", sheet_rgb, tail_a)
    ink_band = np.clip((3.2 - np.abs(dist)) / 1.0, 0, 1) * np.clip((n - 3 - yy) / 2.0, 0, 1)
    ink_band *= np.clip(0.55 + 0.6 * smooth_noise((n, n), (16, 4), rng), 0, 1)
    ink_band[:6, :] = 0.0                                    # open at the top where it joins the sheet
    save_rgba("ui_papyrus_tail_ink", np.broadcast_to(CARBON, (n, n, 3)).copy(), ink_band)

    # Caret: a reed pen stroke, heavy at the top and thinning, with a soft wet edge.
    cw, ch = 16, 64
    yy, xx = np.mgrid[0:ch, 0:cw].astype(np.float32)
    t = yy / (ch - 1.0)
    width = 5.5 * (1.0 - 0.55 * t)
    caret_a = np.clip((width * 0.5 - np.abs(xx - (cw - 1) / 2.0)) / 0.8 + 0.5, 0, 1) * np.clip(np.minimum(yy, ch - 1 - yy) / 3.0, 0, 1)
    save_rgba("ui_ink_caret", np.full((ch, cw, 3), 255, dtype=np.float32), caret_a)

    print("painted bubble textures written to " + OUT)


if __name__ == "__main__":
    sys.exit(main())
