#!/usr/bin/env python3
"""
Generates every texture of Anpu's codex (the journal UI) into ../Resources/RealmCodex/.

    python3 make_codex_art.py            (needs Pillow and numpy)

All art is procedural. Hieroglyph motifs (frieze, cover jackal, seal ankh, icons, numerals) are drawn from
Noto Sans Egyptian Hieroglyphs (SIL OFL 1.1), the font that ships in Art/Glyphs. Textures are power of two.
Some are drawn at their display aspect and squeezed to a power of two width, so Unity stretches them back.
Tintable textures are white on transparent. Gold, papyrus and leather pieces are baked in colour.
Every run uses fixed seeds, so the output is the same each time.
"""
import os
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "Resources", "RealmCodex"))
FONT = os.path.normpath(os.path.join(HERE, "..", "..", "Glyphs", "NotoSansEgyptianHieroglyphs-Regular.ttf"))

GLYPH = {
    "M9": 0x131B8, "R11": 0x132BD, "S40": 0x13300, "S34": 0x132F9, "Y3": 0x133DE, "Y1": 0x133DB,
    "A2": 0x13001, "V10": 0x13377, "D4": 0x13079, "F34": 0x13123, "Z1": 0x133E4, "V20": 0x13386,
    "V1": 0x13362, "E16": 0x130E3, "E15": 0x130E2, "N5": 0x131F3, "M10": 0x131B9,
}


# ---------------------------------------------------------------------------------------------- helpers

def rng(seed):
    return np.random.default_rng(seed)


def resize_f(a, w, h, resample=Image.BICUBIC):
    return np.asarray(Image.fromarray(a.astype(np.float32), mode="F").resize((w, h), resample), dtype=np.float32)


def value_noise(h, w, cell, seed):
    r = rng(seed)
    gh, gw = max(2, int(h / cell) + 2), max(2, int(w / cell) + 2)
    return resize_f(r.random((gh, gw), dtype=np.float32), w, h)


def fbm(h, w, cell, octaves, seed, gain=0.5):
    total = np.zeros((h, w), np.float32)
    amp, norm = 1.0, 0.0
    for i in range(octaves):
        total += amp * value_noise(h, w, max(2.0, cell / (2 ** i)), seed + i * 101)
        norm += amp
        amp *= gain
    return total / norm


def box_blur(a, r):
    if r < 1:
        return a
    r = int(r)
    out = a.astype(np.float32)
    for axis in (0, 1):
        pad = [(0, 0), (0, 0)]
        pad[axis] = (r + 1, r)
        p = np.pad(out, pad, mode="edge")
        c = np.cumsum(p, axis=axis, dtype=np.float64)
        n = out.shape[axis]
        if axis == 0:
            out = ((c[2 * r + 1:2 * r + 1 + n, :] - c[0:n, :]) / (2 * r + 1)).astype(np.float32)
        else:
            out = ((c[:, 2 * r + 1:2 * r + 1 + n] - c[:, 0:n]) / (2 * r + 1)).astype(np.float32)
    return out


def blur(a, sigma):
    r = max(1, int(round(sigma * 0.9)))
    for _ in range(3):
        a = box_blur(a, r)
    return a


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def save_rgba(name, rgb, alpha, size=None):
    rgb = np.clip(rgb, 0, 1)
    alpha = np.clip(alpha, 0, 1)
    arr = np.dstack([rgb, alpha[..., None]]) if rgb.ndim == 3 else np.dstack([rgb, rgb, rgb, alpha])
    img = Image.fromarray((arr * 255 + 0.5).astype(np.uint8), "RGBA")
    if size is not None and img.size != size:
        img = img.resize(size, Image.LANCZOS)
    path = os.path.join(OUT, name)
    img.save(path, optimize=True)
    print("wrote", name, img.size, os.path.getsize(path) // 1024, "KB")


def save_white(name, alpha, size=None):
    h, w = alpha.shape
    save_rgba(name, np.ones((h, w, 3), np.float32), alpha, size)


def glyph_mask(code, px):
    """Tight alpha mask of one hieroglyph rendered with the font at px size."""
    font = ImageFont.truetype(FONT, px)
    ch = chr(GLYPH[code])
    img = Image.new("L", (px * 3, px * 3), 0)
    ImageDraw.Draw(img).text((px, px // 2), ch, font=font, fill=255)
    box = img.getbbox()
    return np.asarray(img.crop(box), dtype=np.float32) / 255.0


def paste_max(dst, src, cx, cy):
    """Paste mask src into dst centred at (cx, cy) with max blending."""
    h, w = src.shape
    x0, y0 = int(round(cx - w / 2)), int(round(cy - h / 2))
    x1, y1 = x0 + w, y0 + h
    sx0, sy0 = max(0, -x0), max(0, -y0)
    x0c, y0c = max(0, x0), max(0, y0)
    x1c, y1c = min(dst.shape[1], x1), min(dst.shape[0], y1)
    if x1c <= x0c or y1c <= y0c:
        return
    dst[y0c:y1c, x0c:x1c] = np.maximum(dst[y0c:y1c, x0c:x1c], src[sy0:sy0 + (y1c - y0c), sx0:sx0 + (x1c - x0c)])


def fit_mask(m, max_w, max_h):
    h, w = m.shape
    k = min(max_w / w, max_h / h)
    return resize_f(m, max(1, int(w * k)), max(1, int(h * k)), Image.LANCZOS).clip(0, 1)


def gold(mask, seed=7, relief=3.0, warm=0.0, flecks=True):
    """Gold leaf shading for an alpha mask: vertical gradient, bevel lit from the upper left, leaf flecks."""
    h, w = mask.shape
    height = blur(mask, relief)
    gy, gx = np.gradient(height)
    light = np.clip(0.55 + (-gx * 0.7 - gy * 0.9) * (14.0 / max(relief, 1)), 0, 1.25)
    v = np.linspace(0, 1, h, dtype=np.float32)[:, None]
    top = np.array([1.00, 0.88, 0.58], np.float32)
    mid = np.array([0.86, 0.64, 0.28], np.float32)
    low = np.array([0.55, 0.36, 0.13], np.float32)
    grad = np.where(v[..., None] < 0.5,
                    top * (1 - v[..., None] * 2) + mid * (v[..., None] * 2),
                    mid * (1 - (v[..., None] - 0.5) * 2) + low * ((v[..., None] - 0.5) * 2))
    grad = np.broadcast_to(grad, (h, w, 3)).copy()
    grad[..., 0] += warm * 0.05
    col = grad * (0.45 + 0.75 * light[..., None])
    spec = np.clip((light - 0.95) * 3.0, 0, 1)[..., None]
    col = col + spec * np.array([1.0, 0.95, 0.80], np.float32) * 0.5
    if flecks:
        n = value_noise(h, w, 3.0, seed)
        col *= (0.92 + 0.16 * n)[..., None]
        crack = smoothstep(0.80, 0.86, fbm(h, w, 24, 3, seed + 5))
        col *= (1 - 0.18 * crack)[..., None]
    return np.clip(col, 0, 1)


def with_shadow(rgb, alpha, offset=(2, 3), sigma=3.0, strength=0.55, color=(0.12, 0.07, 0.03)):
    """Composite a soft drop shadow under an RGBA layer. Returns new rgb, alpha (premultiplied blend)."""
    sh = np.roll(np.roll(blur(alpha, sigma), offset[1], axis=0), offset[0], axis=1) * strength
    out_a = alpha + sh * (1 - alpha)
    c = np.array(color, np.float32)
    out_rgb = (rgb * alpha[..., None] + c * (sh * (1 - alpha))[..., None]) / np.maximum(out_a, 1e-4)[..., None]
    return out_rgb, out_a


# ---------------------------------------------------------------------------------------------- papyrus

def papyrus_page():
    W, H = 1440, 2048
    r = rng(11)
    margin = 20
    # Top layer: horizontal strips. Bottom layer: vertical strips, seen faintly through.
    tone = np.zeros((H, W), np.float32)
    edge_line = np.zeros((H, W), np.float32)
    strip_end = np.zeros(H, np.float32)
    y = 0
    while y < H:
        sw = int(r.integers(28, 64))
        t = r.normal(0, 0.035)
        tone[y:y + sw, :] += t
        edge_line[y:min(H, y + 2), :] = 1.0
        strip_end[y:y + sw] = r.uniform(0, 9)
        y += sw
    xs = np.zeros(W, np.float32)
    vend_top = np.zeros(W, np.float32)
    vend_bot = np.zeros(W, np.float32)
    x = 0
    while x < W:
        sw = int(r.integers(30, 70))
        tone[:, x:x + sw] += r.normal(0, 0.018)
        xs[x:min(W, x + 2)] = 1.0
        vend_top[x:x + sw] = r.uniform(0, 6)
        vend_bot[x:x + sw] = r.uniform(0, 6)
        x += sw
    edge_line = blur(edge_line, 1.2)
    vline = blur(np.broadcast_to(xs[None, :], (H, W)).astype(np.float32), 1.5)

    # Fibres: noise stretched along the strip direction.
    fib_h = resize_f(r.random((H // 2, W // 40), dtype=np.float32), W, H) - 0.5
    fib_h2 = resize_f(r.random((H, W // 90), dtype=np.float32), W, H) - 0.5
    fib_v = resize_f(r.random((H // 40, W // 2), dtype=np.float32), W, H) - 0.5
    low = fbm(H, W, 420, 4, 21)
    mid = fbm(H, W, 90, 3, 31)
    stain = smoothstep(0.62, 0.80, fbm(H, W, 260, 4, 41))

    lum = 1.0 + tone + fib_h * 0.10 + fib_h2 * 0.07 + fib_v * 0.035 + (low - 0.5) * 0.12 + (mid - 0.5) * 0.05
    lum -= edge_line * 0.07
    lum += np.roll(edge_line, 3, axis=0) * 0.03
    lum -= vline * 0.025
    lum -= stain * 0.03

    base = np.array([0.905, 0.815, 0.635], np.float32)
    warm = np.array([0.86, 0.72, 0.50], np.float32)
    mixk = np.clip((low - 0.3) * 1.1 + stain * 0.25, 0, 0.8)[..., None]
    rgb = (base * (1 - mixk) + warm * mixk) * lum[..., None]

    # Specks: fibre nodes, short horizontal dashes.
    speck = np.zeros((H, W), np.float32)
    for _ in range(900):
        sx, sy = int(r.integers(0, W)), int(r.integers(0, H))
        ln = int(r.integers(2, 14))
        speck[sy:sy + 1 + int(r.integers(0, 2)), sx:sx + ln] = r.uniform(0.3, 1.0)
    speck = blur(speck, 0.8)
    rgb *= (1 - 0.22 * speck)[..., None]

    # Deckled outer edges: right (strip ends), top and bottom (vertical strip ends). The spine side (x=0) is straight.
    jit_r = (fbm(H, 1, 14, 4, 51)[:, 0] - 0.5) * 14
    right = (W - margin) - strip_end - jit_r
    jit_t = (fbm(1, W, 14, 4, 61)[0] - 0.5) * 14
    jit_b = (fbm(1, W, 14, 4, 71)[0] - 0.5) * 14
    top = margin + vend_top + jit_t
    bot = (H - margin) - vend_bot - jit_b
    X = np.arange(W, dtype=np.float32)[None, :]
    Y = np.arange(H, dtype=np.float32)[:, None]
    d = np.minimum(np.minimum(right[:, None] - X, Y - top[None, :]), bot[None, :] - Y)
    alpha = np.clip(d + 0.5, 0, 1)
    # A wisp of fibres beyond the edge.
    fringe = np.clip(1 - (-d) / 5.0, 0, 1) * (fib_h > 0.30) * 0.55
    alpha = np.maximum(alpha, fringe * (d < 0))
    # Edge burn: browner and darker near the deckled edges.
    burn = smoothstep(90, 0, d) * (0.6 + 0.4 * low)
    rgb = rgb * (1 - 0.20 * burn)[..., None] + np.array([0.45, 0.28, 0.12], np.float32) * (0.10 * burn)[..., None]
    save_rgba("papyrus_page.png", rgb, alpha, (1024, 2048))


def gutter():
    W, H = 256, 64
    x = np.linspace(0, 1, W, dtype=np.float32)
    a = 0.62 * (1 - x) ** 2.4 + 0.30 * np.exp(-x * 26)
    alpha = np.broadcast_to(a[None, :], (H, W)).astype(np.float32)
    rgb = np.broadcast_to(np.array([0.16, 0.09, 0.04], np.float32), (H, W, 3))
    save_rgba("page_gutter.png", rgb, alpha)


def stack_edge():
    W, H = 64, 1024
    r = rng(81)
    lines = r.random(W).astype(np.float32)
    lum = 0.72 + 0.28 * lines
    lum = np.broadcast_to(lum[None, :], (H, W)).astype(np.float32) * (0.95 + 0.05 * value_noise(H, W, 40, 82))
    save_rgba("stack_edge.png", np.dstack([lum, lum, lum]), np.ones((H, W), np.float32))


def ink_bleed():
    S = 256
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    rr = np.sqrt((xx - S / 2) ** 2 + (yy - S / 2) ** 2) / (S / 2)
    n = fbm(S, S, 40, 4, 91)
    a = smoothstep(0.95, 0.45, rr + (n - 0.5) * 0.55)
    a = blur(a, 3) * (0.75 + 0.25 * fbm(S, S, 12, 2, 92))
    save_white("ink_bleed.png", a)


# ---------------------------------------------------------------------------------------------- gold pieces

def frieze():
    W, H = 1024, 128
    m = np.zeros((H, W), np.float32)
    m[6:11, :] = 1
    m[16:18, :] = 1
    m[H - 11:H - 6, :] = 1
    m[H - 18:H - 16, :] = 1
    order = ["M9", "R11", "S40", "R11"]
    for i in range(8):
        g = fit_mask(glyph_mask(order[i % 4], 200), 92, 80)
        paste_max(m, g, i * 128 + 64, H / 2)
        # small dots between motifs
        for dy in (-14, 0, 14):
            cy, cx = int(H / 2 + dy), i * 128
            m[cy - 2:cy + 3, max(0, cx - 2):cx + 3] = 1
    rgb = gold(m, relief=2.0)
    rgb, a = with_shadow(rgb, m, (1, 2), 1.5, 0.45)
    save_rgba("gold_frieze.png", rgb, a)


def corner_ornament():
    S = 1024
    img = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(img)
    # Double rules along the top and left edges.
    for off, wdt in ((40, 22), (92, 8)):
        d.rectangle([off, off, S - 60, off + wdt], fill=255)
        d.rectangle([off, off, off + wdt, S - 60], fill=255)
    # Lotus bud terminals at the rule ends.
    for (cx, cy, horiz) in ((S - 70, 51, True), (51, S - 70, False)):
        if horiz:
            d.polygon([(cx, cy - 26), (cx + 50, cy), (cx, cy + 26)], fill=255)
        else:
            d.polygon([(cx - 26, cy), (cx, cy + 50), (cx + 26, cy)], fill=255)
    # Quarter rosette at the corner: ring plus petals.
    c = 200
    d.ellipse([c - 120, c - 120, c + 120, c + 120], outline=255, width=14)
    d.ellipse([c - 46, c - 46, c + 46, c + 46], fill=255)
    for k in range(12):
        ang = k * math.pi / 6
        px, py = c + math.cos(ang) * 84, c + math.sin(ang) * 84
        pts = []
        for t in range(24):
            u = t / 23 * 2 * math.pi
            ex, ey = math.cos(u) * 30, math.sin(u) * 11
            pts.append((px + ex * math.cos(ang) - ey * math.sin(ang), py + ex * math.sin(ang) + ey * math.cos(ang)))
        d.polygon(pts, fill=255)
    # Diagonal lotus flower reaching into the page.
    lotus = fit_mask(glyph_mask("M9", 400), 260, 260)
    lotus = np.asarray(Image.fromarray((lotus * 255).astype(np.uint8)).rotate(45, expand=True, resample=Image.BICUBIC), np.float32) / 255
    m = np.asarray(img, np.float32) / 255
    paste_max(m, lotus, 400, 400)
    m = resize_f(m, 256, 256, Image.LANCZOS).clip(0, 1)
    rgb = gold(m, relief=1.5)
    rgb, a = with_shadow(rgb, m, (1, 2), 1.5, 0.5)
    save_rgba("corner_ornament.png", rgb, a)


def seal():
    S = 512
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    ang = np.arctan2(yy - S / 2, xx - S / 2)
    rr = np.sqrt((xx - S / 2) ** 2 + (yy - S / 2) ** 2)
    edge = 200 + 10 * np.sin(ang * 7 + 1.3) + 6 * np.sin(ang * 13) + (fbm(S, S, 30, 2, 111) - 0.5) * 16
    disc = np.clip(edge - rr, 0, 1)
    rim = smoothstep(0, 10, edge - rr) * smoothstep(48, 30, edge - rr)
    ankh = np.zeros((S, S), np.float32)
    paste_max(ankh, fit_mask(glyph_mask("S34", 400), 210, 250), S / 2, S / 2)
    ankh = np.clip(blur(ankh, 3) * 2.5, 0, 1)
    height = disc * 0.35 + rim * 0.45 - ankh * 0.35 + ankh * smoothstep(0, 1, ankh) * 0.0
    rgb = gold(height.clip(0, 1), relief=3, warm=1.0)
    rgb *= (1 - 0.45 * ankh)[..., None]
    rgb *= (0.85 + 0.15 * disc)[..., None]
    rgb, a = with_shadow(rgb, disc, (4, 8), 8, 0.5)
    save_rgba("seal.png", rgb, a, (128, 128))


# ---------------------------------------------------------------------------------------------- leather

def leather(W, H, seed):
    pebble = np.abs(fbm(H, W, 14, 3, seed) - 0.5) * 2
    pebble = blur(pebble, 1.2)
    gy, gx = np.gradient(pebble)
    light = np.clip(0.6 + (-gx - gy) * 6, 0, 1.3)
    low = fbm(H, W, 300, 4, seed + 9)
    base = np.array([0.20, 0.115, 0.07], np.float32)
    deep = np.array([0.10, 0.055, 0.035], np.float32)
    k = np.clip(low * 1.2 - 0.1, 0, 1)[..., None]
    rgb = (deep * (1 - k) + base * k) * (0.75 + 0.45 * light)[..., None]
    # Wear: lighter near the borders, rubbed.
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    dist = np.minimum(np.minimum(xx, W - 1 - xx), np.minimum(yy, H - 1 - yy))
    wear = smoothstep(40, 0, dist) * (0.5 + 0.5 * fbm(H, W, 60, 2, seed + 3))
    rgb = rgb + np.array([0.20, 0.12, 0.06], np.float32) * wear[..., None] * 0.6
    return rgb, dist


def cover():
    W, H = 1440, 2048
    rgb, dist = leather(W, H, 201)
    # Spine hinge at the left edge: a pressed groove and raised bands.
    xx = np.arange(W, dtype=np.float32)[None, :]
    hinge = smoothstep(110, 70, xx) * 0.35
    groove = np.exp(-((xx - 96) ** 2) / 60) * 0.5
    rgb *= (1 - hinge - groove)[..., None]
    m = np.zeros((H, W), np.float32)
    # Tooled gold frame: double rule.
    for inset, wdt in ((150, 12), (180, 5)):
        x0, y0, x1, y1 = inset, inset - 60, W - inset + 40, H - inset + 60
        m[y0:y0 + wdt, x0:x1] = 1
        m[y1 - wdt:y1, x0:x1] = 1
        m[y0:y1, x0:x0 + wdt] = 1
        m[y0:y1, x1 - wdt:x1] = 1
    # Frieze bands across the top and bottom inside the frame.
    fr = np.zeros((128, 1024), np.float32)
    order = ["M9", "R11", "S40", "R11"]
    for i in range(8):
        paste_max(fr, fit_mask(glyph_mask(order[i % 4], 200), 92, 80), i * 128 + 64, 64)
    fr = resize_f(fr, 920, 115, Image.LANCZOS).clip(0, 1)
    paste_max(m, fr, (W + 80) / 2, 260)
    paste_max(m, fr, (W + 80) / 2, H - 260)
    # Anpu on his shrine, the giver of the book, with a sun disk above.
    jackal = fit_mask(glyph_mask("E16", 900), 760, 620)
    paste_max(m, jackal, (W + 80) / 2, H * 0.50)
    sun = fit_mask(glyph_mask("N5", 400), 200, 200)
    paste_max(m, sun, (W + 80) / 2, H * 0.27)
    ankh = fit_mask(glyph_mask("S34", 400), 120, 200)
    paste_max(m, ankh, (W + 80) / 2, H * 0.73)
    g = gold(m, relief=4.0)
    # Debossed: a dark line around the gold.
    halo = np.clip(blur(m, 3) * 1.6 - m, 0, 1)
    rgb = rgb * (1 - 0.6 * halo)[..., None]
    rgb = rgb * (1 - m[..., None]) + g * m[..., None]
    alpha = np.ones((H, W), np.float32)
    # Rounded outer corners on the fore edge.
    yy, xx2 = np.mgrid[0:H, 0:W].astype(np.float32)
    rad = 40
    for cy in (rad, H - 1 - rad):
        cx = W - 1 - rad
        mask = ((xx2 > cx) & ((yy < rad) if cy == rad else (yy > H - 1 - rad)))
        dd = np.sqrt((xx2 - cx) ** 2 + (yy - cy) ** 2)
        alpha = np.where(mask, np.clip(rad - dd + 0.5, 0, 1), alpha)
    save_rgba("cover_leather.png", rgb, alpha, (1024, 2048))
    return rgb, alpha


def board():
    W, H = 512, 1024
    rgb, dist = leather(W, H, 301)
    line = ((dist > 9) & (dist < 12)).astype(np.float32)
    g = gold(line, relief=1.0, flecks=False)
    rgb = rgb * (1 - line[..., None]) + g * line[..., None]
    save_rgba("board_leather.png", rgb, np.ones((H, W), np.float32))


def codex_icon(cover_rgb, cover_a):
    S = 512
    out_rgb = np.zeros((S, S, 3), np.float32)
    out_a = np.zeros((S, S), np.float32)
    bw, bh = 300, 410
    x0, y0 = (S - bw) // 2 - 16, (S - bh) // 2
    # Page block peeking out on the fore edge and bottom.
    for k in range(3, 0, -1):
        px0, py0 = x0 + 6 * k + 4, y0 + 5 * k
        out_rgb[py0:py0 + bh, px0:px0 + bw] = np.array([0.86, 0.77, 0.58]) * (0.85 + 0.05 * k)
        out_a[py0:py0 + bh, px0:px0 + bw] = 1
    cov = resize_f(cover_rgb[..., 0], bw, bh), resize_f(cover_rgb[..., 1], bw, bh), resize_f(cover_rgb[..., 2], bw, bh)
    cov = np.dstack(cov)
    ca = resize_f(cover_a, bw, bh)
    region = (slice(y0, y0 + bh), slice(x0, x0 + bw))
    out_rgb[region] = cov * ca[..., None] + out_rgb[region] * (1 - ca[..., None])
    out_a[region] = np.maximum(out_a[region], ca)
    out_rgb, out_a = with_shadow(out_rgb, out_a, (6, 10), 10, 0.6, (0, 0, 0))
    save_rgba("codex_icon.png", out_rgb, out_a, (256, 256))


# ---------------------------------------------------------------------------------------------- tintable UI

def icons():
    for name, code in (("icon_reed.png", "Y3"), ("kind_word.png", "A2"), ("kind_name.png", "V10"),
                       ("kind_lore.png", "Y1"), ("kind_clue.png", "D4"), ("kind_memory.png", "F34")):
        m = np.zeros((256, 256), np.float32)
        paste_max(m, fit_mask(glyph_mask(code, 400), 230, 230), 128, 128)
        save_white(name, m, (128, 128))
    # Egyptian numerals: stroke (1), heel bone (10), coil (100). Cropped tight, 128 px tall.
    for name, code in (("num_1.png", "Z1"), ("num_10.png", "V20"), ("num_100.png", "V1")):
        g = glyph_mask(code, 600)
        h, w = g.shape
        k = 120 / h
        g = resize_f(g, max(8, int(w * k)), 120, Image.LANCZOS).clip(0, 1)
        cw = 1 << int(math.ceil(math.log2(g.shape[1] + 8)))
        m = np.zeros((128, cw), np.float32)
        paste_max(m, g, cw / 2, 64)
        save_white(name, m)


def divider():
    W, H = 1024, 64
    m = np.zeros((H, W), np.float32)
    x = np.arange(W, dtype=np.float32)
    fade = smoothstep(40, 300, x) * smoothstep(W - 40, W - 300, x)
    gap = 1 - smoothstep(60, 40, np.abs(x - W / 2))
    line = (fade * gap)[None, :]
    m[30:33, :] = np.maximum(m[30:33, :], line)
    lotus = fit_mask(glyph_mask("M9", 300), 60, 56)
    paste_max(m, lotus, W / 2, H / 2)
    for dx in (-80, 80):
        yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
        m = np.maximum(m, np.clip(4.5 - np.sqrt((xx - W / 2 - dx) ** 2 + (yy - 31.5) ** 2), 0, 1))
    save_white("divider.png", m)


def card_frame():
    S = 256
    img = Image.new("L", (S * 4, S * 4), 0)
    d = ImageDraw.Draw(img)
    k = 4
    d.rectangle([16 * k, 16 * k, (S - 16) * k, (S - 16) * k], outline=255, width=3 * k)
    d.rectangle([24 * k, 24 * k, (S - 24) * k, (S - 24) * k], outline=150, width=1 * k)
    for cx, cy in ((16, 16), (S - 16, 16), (16, S - 16), (S - 16, S - 16)):
        d.rectangle([(cx - 7) * k, (cy - 7) * k, (cx + 7) * k, (cy + 7) * k], fill=255)
        d.rectangle([(cx - 3) * k, (cy - 3) * k, (cx + 3) * k, (cy + 3) * k], fill=0)
    m = np.asarray(img.resize((S, S), Image.LANCZOS), np.float32) / 255
    save_white("card_frame.png", m)


def keycap():
    W, H = 128, 64
    img = Image.new("L", (W * 4, H * 4), 0)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([4, 4, W * 4 - 5, H * 4 - 5], radius=56, fill=40, outline=255, width=10)
    save_white("keycap.png", np.asarray(img.resize((W, H), Image.LANCZOS), np.float32) / 255)


def toast_band():
    W, H = 512, 128
    x = np.linspace(0, 1, W, dtype=np.float32)
    y = np.linspace(0, 1, H, dtype=np.float32)
    a = smoothstep(0.0, 0.55, x)[None, :] * (smoothstep(0.0, 0.12, y) * smoothstep(1.0, 0.88, y))[:, None] * 0.90
    n = value_noise(H, W, 6, 401)
    rgb = np.dstack([np.full((H, W), 0.075), np.full((H, W), 0.052), np.full((H, W), 0.036)]).astype(np.float32)
    rgb *= (0.9 + 0.2 * n)[..., None]
    save_rgba("toast_band.png", rgb, a)


def vignette():
    S = 512
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    rr = np.sqrt(((xx - S / 2) / (S / 2)) ** 2 + ((yy - S / 2) / (S / 2)) ** 2)
    a = smoothstep(0.35, 1.30, rr) * 0.95
    save_rgba("vignette.png", np.zeros((S, S, 3), np.float32), a)


def soft_shadow():
    S = 256
    m = np.zeros((S, S), np.float32)
    m[72:S - 72, 72:S - 72] = 1
    a = blur(m, 30)
    save_rgba("book_shadow.png", np.zeros((S, S, 3), np.float32), a)


def corner_curl():
    """A lifted page corner (bottom right of a right page), shown on hover."""
    S = 256
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    flap = (xx + yy > S * 1.0) & (xx < S) & (yy < S)
    # The underside of the flap: lighter papyrus, gradient across the fold.
    t = np.clip((xx + yy - S) / S, 0, 1)
    col = np.array([0.93, 0.86, 0.70], np.float32) * (0.82 + 0.18 * t)[..., None]
    a = flap.astype(np.float32)
    a = blur(a, 0.8)
    # The shadow it casts on the page along the fold.
    fold = np.exp(-((xx + yy - S) ** 2) / 300.0) * (xx + yy < S)
    rgb = col * a[..., None]
    out_a = np.maximum(a, fold * 0.45)
    rgb = np.where(a[..., None] > 0.01, col, np.array([0.15, 0.09, 0.04], np.float32))
    save_rgba("corner_curl.png", rgb, out_a, (128, 128))


def memory_placeholder():
    W, H = 1024, 512
    r = rng(501)
    paper = np.array([0.80, 0.67, 0.47], np.float32)
    ink = np.array([0.36, 0.22, 0.11], np.float32)
    low = fbm(H, W, 160, 4, 502)
    img = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(img)
    hz = int(H * 0.56)
    # Sun disk with rays.
    d.ellipse([W * 0.62 - 54, hz - 150, W * 0.62 + 54, hz - 42], fill=110)
    # Far bank and distant pyramid.
    d.polygon([(0, hz), (W * 0.2, hz - 14), (W * 0.45, hz - 6), (W, hz - 18), (W, hz + 4), (0, hz + 6)], fill=150)
    d.polygon([(W * 0.30, hz - 6), (W * 0.36, hz - 70), (W * 0.42, hz - 8)], fill=120)
    # Water ripples.
    for i in range(16):
        yv = hz + 22 + i * 13
        x0 = int(r.uniform(0, W * 0.6))
        d.line([(x0, yv), (x0 + int(r.uniform(80, 340)), yv)], fill=int(90 + i * 4), width=2)
    # Reed clumps in the foreground.
    for base in (W * 0.08, W * 0.16, W * 0.82, W * 0.9):
        for _ in range(22):
            h = r.uniform(120, 260)
            lean = r.uniform(-50, 50)
            bx = base + r.uniform(-30, 30)
            d.line([(bx, H), (bx + lean, H - h)], fill=200, width=3)
            d.ellipse([bx + lean - 7, H - h - 20, bx + lean + 7, H - h + 4], fill=200)
    wash = blur(np.asarray(img, np.float32) / 255, 1.5)
    wash = wash * (0.75 + 0.5 * low)
    rgb = paper * (0.9 + 0.2 * low)[..., None] * (1 - wash[..., None]) + ink * wash[..., None]
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    rr = np.sqrt(((xx - W / 2) / (W / 2)) ** 2 + ((yy - H / 2) / (H / 2)) ** 2)
    edge = smoothstep(1.25, 0.80, rr + (fbm(H, W, 40, 3, 503) - 0.5) * 0.3)
    rgb *= (0.8 + 0.2 * edge)[..., None]
    save_rgba("memory_placeholder.png", rgb, edge)


def main():
    os.makedirs(OUT, exist_ok=True)
    papyrus_page()
    gutter()
    stack_edge()
    ink_bleed()
    frieze()
    corner_ornament()
    seal()
    crgb, ca = cover()
    board()
    codex_icon(crgb, ca)
    icons()
    divider()
    card_frame()
    keycap()
    toast_band()
    vignette()
    soft_shadow()
    corner_curl()
    memory_placeholder()


if __name__ == "__main__":
    main()
