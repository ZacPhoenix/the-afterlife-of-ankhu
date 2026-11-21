#!/usr/bin/env python3
"""Offline generator for the Realm glyph and particle textures.

Renders Gardiner signs from NotoSansEgyptianHieroglyphs-Regular.ttf (SIL OFL) white on transparent
into ../Resources/RealmGlyphs/<code>.png with a blurred <code>_glow.png twin, plus the cartouche
frame, particle sprites and bubble UI pieces. It also writes the code table that the runtime reads
(../../../Runtime/Magic/GlyphTable.cs and ../Resources/RealmGlyphs/glyph_table.json).

All textures are power of two on both axes so Unity's default import (non power of two scaling to
nearest) can never change their aspect. Glyphs share one em box and one baseline: a 256 px tall
canvas, so small signs stay small and sit where a scribe would put them.

Usage: python3 make_glyphs.py   (needs Pillow, numpy, fonttools)
"""
import json
import os
import re
import sys
import unicodedata

import numpy as np
from fontTools.ttLib import TTFont
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
GLYPHS_DIR = os.path.dirname(HERE)
FONT_PATH = os.path.join(GLYPHS_DIR, "NotoSansEgyptianHieroglyphs-Regular.ttf")
OUT = os.path.join(GLYPHS_DIR, "Resources", "RealmGlyphs")
CS_OUT = os.path.join(GLYPHS_DIR, "..", "..", "Runtime", "Magic", "GlyphTable.cs")

CODES = """E34 N35 O31 S20 Aa1 X1 G17 D21 O34 D4 R4 Q3 S34 G43 P1 N35A G5 E15 E16 F34 H6 N5 D28 G53 A1 B1
A17 A2 M17 G1 V31 Z1 D36 N1 N14 O1 O49 Q7 R8 Y1 W24 V28 U15 S29 F35 D2 D45 V13 I9 N37 O39 Aa15 N29
E23 F31 C10 C7 C3 C2 G7 G26 G25 G39 I10 L1 M23 M8 N8 O4 P8 R11 S42 T28 U1 V30 W11 X8 Z2 Z3 Z7
D46 D58 G35 I14 M16 N25 N31 O28 Q1 R7 S3 S12 S28 S38 T14 U33 V4 V20 W15 W19 Y5 Aa11 Aa13 F40 F12 F13
H8 L2 M13 M42 N16 N17 N33 O50 R14 S22 T22 U28 V24 W9 W25 Z4
V10 N31 F21 V17 A40 Q1 U2 D37 U38 V1 D58 D46 O18 A17 P1 E15 G5 F35 B1 M17""".split()
CODES = list(dict.fromkeys(CODES))  # keep first occurrence order, drop repeats

H = 256          # canvas height (em box) for every sign
PAD = 26         # transparent margin inside the canvas, room for the glow
GLOW_RADIUS = 9
STROKE_GROW = 5   # MaxFilter size at 2x: about 1 px extra stroke each side


def unicode_name(code):
    m = re.fullmatch(r"([A-Z][a-z]?)(\d+)([A-Za-z]?)", code)
    if not m:
        raise ValueError(code)
    prefix, num, suffix = m.groups()
    return "EGYPTIAN HIEROGLYPH %s%03d%s" % (prefix.upper(), int(num), suffix.upper())


def pot(n):
    p = 1
    while p < n:
        p *= 2
    return p


def white_rgba(alpha):
    """White RGB everywhere (no dark fringes under bilinear filtering), alpha from the mask."""
    a = np.asarray(alpha, dtype=np.uint8)
    rgba = np.zeros(a.shape + (4,), dtype=np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = a
    return Image.fromarray(rgba, "RGBA")


def glow_of(alpha_img, radius):
    a = alpha_img.filter(ImageFilter.MaxFilter(5))
    wide = a.filter(ImageFilter.GaussianBlur(radius * 1.6))
    tight = a.filter(ImageFilter.GaussianBlur(radius * 0.55))
    w = np.asarray(wide, dtype=np.float32) / 255.0
    t = np.asarray(tight, dtype=np.float32) / 255.0
    g = np.clip(0.75 * w + 0.55 * t, 0, 1)
    g = np.power(g, 0.8)
    return Image.fromarray((g * 255).astype(np.uint8), "L")


def render_glyphs():
    tt = TTFont(FONT_PATH)
    cmap = tt.getBestCmap()
    entries, missing = [], []
    chars = {}
    for code in CODES:
        try:
            ch = unicodedata.lookup(unicode_name(code))
        except KeyError:
            missing.append(code + " (no Unicode name)")
            continue
        if ord(ch) not in cmap:
            missing.append(code + " (not in font)")
            continue
        chars[code] = ch

    # One font size for all signs: the union of ink extents must fit inside the padded em box.
    probe = ImageFont.truetype(FONT_PATH, 200)
    top, bottom = 0, 0
    for ch in chars.values():
        l, t, r, b = probe.getbbox(ch, anchor="ls")
        top, bottom = min(top, t), max(bottom, b)
    size = int(200 * (H - 2 * PAD) / float(bottom - top))
    font = ImageFont.truetype(FONT_PATH, size)
    font2 = ImageFont.truetype(FONT_PATH, size * 2)
    top, bottom = 0, 0
    for ch in chars.values():
        l, t, r, b = font.getbbox(ch, anchor="ls")
        top, bottom = min(top, t), max(bottom, b)
    baseline = PAD - top + ((H - 2 * PAD) - (bottom - top)) // 2

    for code, ch in chars.items():
        l, t, r, b = font.getbbox(ch, anchor="ls")
        ink_w = r - l
        w = pot(ink_w + 2 * PAD)
        x0 = (w - ink_w) // 2 - l
        # Noto draws signs as fine outlines. Render at 2x, thicken the stroke a little so the signs
        # still read when the bubble shows them small, then downsample for smooth edges.
        big = Image.new("L", (w * 2, H * 2), 0)
        ImageDraw.Draw(big).text((x0 * 2, baseline * 2), ch, font=font2, fill=255, anchor="ls")
        big = big.filter(ImageFilter.MaxFilter(STROKE_GROW))
        mask = big.resize((w, H), Image.LANCZOS)
        bbox = mask.getbbox()
        if bbox is None:
            missing.append(code + " (empty)")
            continue
        white_rgba(mask).save(os.path.join(OUT, code + ".png"), optimize=True)
        white_rgba(glow_of(mask, GLOW_RADIUS)).save(os.path.join(OUT, code + "_glow.png"), optimize=True)
        bx0, by0, bx1, by1 = bbox
        entries.append({
            "code": code,
            "codepoint": "U+%05X" % ord(ch),
            "char": ch,
            "w": w, "h": H,
            # Ink box in normalised texture space, y measured up from the bottom like Unity.
            "inkL": round(bx0 / w, 5), "inkR": round(bx1 / w, 5),
            "inkB": round((H - by1) / H, 5), "inkT": round((H - by0) / H, 5),
        })
    return entries, missing


# ------------------------------------------------------------------ frames, particles and UI pieces

def ss(img_size, scale=4):
    return (img_size[0] * scale, img_size[1] * scale)


def down(img, size):
    return img.resize(size, Image.LANCZOS)


def cartouche():
    """Horizontal cartouche: a stadium of twisted rope, tied off by a bar at the right end."""
    W, Hc, S = 512, 256, 4
    big = Image.new("L", (W * S, Hc * S), 0)
    d = ImageDraw.Draw(big)
    m = 14 * S
    bar_gap = 26 * S
    x0, y0, x1, y1 = m, m, W * S - m - bar_gap, Hc * S - m
    stroke = 11 * S
    d.rounded_rectangle([x0, y0, x1, y1], radius=(y1 - y0) // 2, outline=255, width=stroke)
    inner = 5 * S
    d.rounded_rectangle([x0 + stroke + inner, y0 + stroke + inner, x1 - stroke - inner, y1 - stroke - inner],
                        radius=(y1 - y0) // 2 - stroke - inner, outline=150, width=2 * S)
    # The tie bar: a short vertical bar standing just past the rope's right end.
    bx = x1 + bar_gap // 2 + 2 * S
    d.rounded_rectangle([bx - 6 * S, y0 + 12 * S, bx + 6 * S, y1 - 12 * S], radius=5 * S, fill=255)
    d.rectangle([x1 - 2 * S, Hc * S // 2 - 5 * S, bx, Hc * S // 2 + 5 * S], fill=255)
    a = np.asarray(big, dtype=np.float32)
    # Rope twist: diagonal bands cut gently into the stroke.
    yy, xx = np.mgrid[0:Hc * S, 0:W * S]
    twist = 0.78 + 0.22 * np.cos((xx + yy) * (2 * np.pi / (16.0 * S)))
    a = a * twist
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "L")
    img = down(img, (W, Hc))
    white_rgba(img).save(os.path.join(OUT, "cartouche.png"), optimize=True)
    white_rgba(glow_of(img, 8)).save(os.path.join(OUT, "cartouche_glow.png"), optimize=True)
    return {"w": W, "h": Hc, "left": 130, "right": 168, "top": 127, "bottom": 127}


def radial(n, fn):
    c = (n - 1) / 2.0
    yy, xx = np.mgrid[0:n, 0:n]
    r = np.sqrt((xx - c) ** 2 + (yy - c) ** 2) / c
    ang = np.arctan2(yy - c, xx - c)
    return np.clip(fn(r, ang), 0, 1)


def save_alpha(name, a):
    white_rgba((a * 255).astype(np.uint8)).save(os.path.join(OUT, name + ".png"), optimize=True)


def particles():
    n = 128
    save_alpha("fx_mote", radial(n, lambda r, a: np.power(np.clip(1 - r, 0, 1), 2.2)))
    save_alpha("fx_spark", radial(n, lambda r, a:
        np.clip(np.power(np.clip(1 - r, 0, 1), 6) * 1.2
                + np.power(np.abs(np.cos(2 * a)), 60) * np.power(np.clip(1 - r, 0, 1), 1.5) * 0.9
                + np.power(np.abs(np.cos(2 * a + np.pi / 4)), 90) * np.power(np.clip(1 - r * 1.6, 0, 1), 2) * 0.45, 0, 1)))
    save_alpha("fx_ring", radial(n, lambda r, a: (np.exp(-((r - 0.78) / 0.07) ** 2) + 0.15 * np.exp(-((r - 0.78) / 0.2) ** 2)) * np.clip((1 - r) / 0.12, 0, 1)))
    save_alpha("fx_flare", radial(256, lambda r, a: np.power(np.clip(1 - r, 0, 1), 3.5)
                                  + 0.35 * np.power(np.abs(np.cos(a)), 200) * np.clip(1 - r, 0, 1)))
    # Smoke wisp: soft noise blob with a curl.
    rng = np.random.default_rng(7)
    base = rng.random((16, 16)).astype(np.float32)
    noise = np.asarray(Image.fromarray((base * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC), dtype=np.float32) / 255
    fine = rng.random((32, 32)).astype(np.float32)
    noise2 = np.asarray(Image.fromarray((fine * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC), dtype=np.float32) / 255
    blob = radial(n, lambda r, a: np.power(np.clip(1 - r, 0, 1), 1.4))
    smoke = np.clip(blob * (0.35 + 0.8 * noise * (0.6 + 0.6 * noise2)) * 1.1, 0, 1)
    smoke = np.asarray(Image.fromarray((smoke * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(3)), dtype=np.float32) / 255
    save_alpha("fx_smoke", smoke)
    # Light shaft: vertical beam, bright in the middle, fading to the top.
    w, h = 64, 256
    yy, xx = np.mgrid[0:h, 0:w]
    u = np.abs(xx - (w - 1) / 2) / ((w - 1) / 2)
    v = 1 - yy / (h - 1.0)  # 0 at top row, 1 at bottom row
    shaft = np.power(np.clip(1 - u, 0, 1), 2.0) * np.power(v, 1.3) * np.clip(v * 6, 0, 1)
    save_alpha("fx_shaft", shaft)
    # Sand grain: tiny hard dot.
    save_alpha("fx_grain", radial(32, lambda r, a: np.clip((1 - r) * 3, 0, 1)))


def ui_pieces():
    # Rounded panel (fill) and hairline border, 9 sliced at radius 24 px.
    n, rad, S = 128, 24, 4
    big = Image.new("L", (n * S, n * S), 0)
    ImageDraw.Draw(big).rounded_rectangle([0, 0, n * S - 1, n * S - 1], radius=rad * S, fill=255)
    white_rgba(down(big, (n, n))).save(os.path.join(OUT, "ui_panel.png"), optimize=True)
    big = Image.new("L", (n * S, n * S), 0)
    ImageDraw.Draw(big).rounded_rectangle([S, S, n * S - 1 - S, n * S - 1 - S], radius=rad * S - S, outline=255, width=int(1.5 * S))
    white_rgba(down(big, (n, n))).save(os.path.join(OUT, "ui_border.png"), optimize=True)
    # Soft drop shadow for the panel, 9 sliced at 48 px.
    n2 = 256
    big = Image.new("L", (n2, n2), 0)
    ImageDraw.Draw(big).rounded_rectangle([48, 48, n2 - 49, n2 - 49], radius=28, fill=255)
    white_rgba(big.filter(ImageFilter.GaussianBlur(16))).save(os.path.join(OUT, "ui_shadow.png"), optimize=True)
    # Tail: a slim isoceles triangle pointing down, apex at bottom centre.
    w, h = 64, 64
    big = Image.new("L", (w * S, h * S), 0)
    ImageDraw.Draw(big).polygon([(0, 0), (w * S - 1, 0), (w * S / 2, h * S - 1)], fill=255)
    white_rgba(down(big, (w, h))).save(os.path.join(OUT, "ui_tail.png"), optimize=True)
    # Shimmer band: horizontal soft streak used for the moving gold sheen.
    w, h = 256, 32
    yy, xx = np.mgrid[0:h, 0:w]
    u = (xx - (w - 1) / 2) / ((w - 1) / 2)
    v = (yy - (h - 1) / 2) / ((h - 1) / 2)
    band = np.exp(-(u / 0.35) ** 2) * (1 - np.abs(v) * 0.2)
    save_alpha("ui_sheen", np.clip(band, 0, 1))
    # Horizontal hairline that fades out at both ends.
    w, h = 256, 4
    yy, xx = np.mgrid[0:h, 0:w]
    u = np.abs(xx - (w - 1) / 2) / ((w - 1) / 2)
    save_alpha("ui_hairline", np.clip(1 - np.power(u, 2.5), 0, 1) * np.ones_like(yy))


def write_tables(entries, cart):
    with open(os.path.join(OUT, "glyph_table.json"), "w", encoding="utf-8") as f:
        json.dump({"source": "Noto Sans Egyptian Hieroglyphs 2.002 (SIL OFL 1.1)", "em": H,
                   "cartouche": cart, "glyphs": entries}, f, ensure_ascii=False, indent=1)
    lines = []
    for e in entries:
        lines.append('            new GlyphInfo("%s", 0x%s, %d, %d, %.5ff, %.5ff, %.5ff, %.5ff),'
                     % (e["code"], e["codepoint"][2:], e["w"], e["h"], e["inkL"], e["inkR"], e["inkB"], e["inkT"]))
    cs = """// Generated by Art/Glyphs/_tools/make_glyphs.py. Do not edit by hand, run the script again.
// Glyph art: Noto Sans Egyptian Hieroglyphs (SIL Open Font License 1.1).
namespace Ankhu.Realms
{
    /// <summary>One rendered sign: its Gardiner code, Unicode code point, texture size and ink box (normalised, y up).</summary>
    public readonly struct GlyphInfo
    {
        public readonly string Code;
        public readonly int CodePoint;
        public readonly int Width;
        public readonly int Height;
        public readonly float InkLeft;
        public readonly float InkRight;
        public readonly float InkBottom;
        public readonly float InkTop;

        public GlyphInfo(string code, int codePoint, int width, int height, float inkLeft, float inkRight, float inkBottom, float inkTop)
        {
            Code = code;
            CodePoint = codePoint;
            Width = width;
            Height = height;
            InkLeft = inkLeft;
            InkRight = inkRight;
            InkBottom = inkBottom;
            InkTop = inkTop;
        }

        /// <summary>The sign as a UTF-16 string (a surrogate pair).</summary>
        public string Text => char.ConvertFromUtf32(CodePoint);
    }

    /// <summary>Gardiner code to Unicode table for every sign shipped under Resources/RealmGlyphs.</summary>
    public static class GlyphTable
    {
        /// <summary>Height in pixels of every glyph texture (the shared em box).</summary>
        public const int EmPixels = %d;

        /// <summary>Cartouche frame 9 slice borders in pixels (left, bottom, right, top) on a %dx%d texture.</summary>
        public static readonly UnityEngine.Vector4 CartoucheBorder = new UnityEngine.Vector4(%d, %d, %d, %d);

        public static readonly GlyphInfo[] All =
        {
%s
        };
    }
}
""" % (H, cart["w"], cart["h"], cart["left"], cart["bottom"], cart["right"], cart["top"], "\n".join(lines))
    with open(CS_OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(cs)


def main():
    os.makedirs(OUT, exist_ok=True)
    entries, missing = render_glyphs()
    cart = cartouche()
    particles()
    ui_pieces()
    write_tables(entries, cart)
    print("rendered %d signs" % len(entries))
    if missing:
        print("skipped: " + ", ".join(missing))
    else:
        print("skipped: none")


if __name__ == "__main__":
    sys.exit(main())
