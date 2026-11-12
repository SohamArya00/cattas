"""Draws the kitten sprite sheet (based on Codi's seal-point cat): 6 frames of 48x40.
Frames: 0-3 walk cycle, 4 falling, 5 sitting. Saved as Assets/Resources/Art/cat_sheet.bytes (PNG data)."""
import os
from PIL import Image, ImageDraw, ImageFilter
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Art")

# ---------------------------------------------------------------- kitten sprite sheet (your cat!)
FW, FH, CS = 48, 40, 8           # frame size at art scale, supersample
CREAM = (246, 234, 214, 255); SHADE = (226, 206, 182, 255); POINT = (96, 66, 54, 255); POINT_L = (138, 100, 80, 255)
WHITE = (255, 252, 246, 255); EYE = (110, 182, 236, 255)

def kitten(legs, tail_up=0.0, ears_up=0.0, mouth_open=False):
    im = Image.new("RGBA", (FW * CS, FH * CS), (0, 0, 0, 0)); dd = ImageDraw.Draw(im)
    def e(cx, cy, rx, ry, col): dd.ellipse([(cx - rx) * CS, (cy - ry) * CS, (cx + rx) * CS, (cy + ry) * CS], fill=col)
    def poly(pts, col): dd.polygon([(x * CS, y * CS) for x, y in pts], fill=col)
    # tail (fluffy, dark)
    for i in range(7):
        t = i / 6
        tx = 12 - 7 * t; ty = 24 - (10 + 6 * tail_up) * t * t - 2 * t
        e(tx, ty, 3.4 - t * 0.6, 3.4 - t * 0.6, POINT if t > 0.3 else POINT_L)
    # back legs, body, front legs
    for lx, ly in legs[:2]: e(lx, 33 + ly * 0.3, 3, 5, SHADE); e(lx + 0.5, 37.2 + ly * 0.2, 3.1, 2.2, POINT)
    e(22, 26, 13, 9, CREAM); e(22, 29, 11, 5, SHADE); e(20, 22, 10, 5, CREAM)
    for lx, ly in legs[2:]: e(lx, 33 + ly * 0.3, 3, 5, CREAM); e(lx + 0.5, 37.2 + ly * 0.2, 3.1, 2.2, POINT)
    e(31, 26, 6, 6, WHITE)                                     # fluffy chest
    # head
    hx, hy = 34, 15
    poly([(hx - 9, hy - 3), (hx - 7, hy - 14 - ears_up), (hx - 1, hy - 7)], POINT)
    poly([(hx + 2, hy - 7), (hx + 8, hy - 14 - ears_up), (hx + 10, hy - 2)], POINT)
    poly([(hx - 7, hy - 5), (hx - 6.3, hy - 11 - ears_up), (hx - 3, hy - 7)], (236, 160, 170, 255))
    e(hx, hy, 11, 10, CREAM)
    e(hx + 2, hy + 3.5, 7.5, 5.5, POINT_L); e(hx + 2.5, hy + 4.5, 5.5, 4, POINT)   # seal-point mask
    for ex in (hx - 3.2, hx + 5.2):
        e(ex, hy - 0.5, 2.9, 3.2, EYE); e(ex + 0.4, hy - 0.2, 1.1, 2.2, (30, 30, 40, 255)); e(ex - 0.8, hy - 1.6, 0.9, 0.9, WHITE)
    e(hx + 2.5, hy + 4.2, 1.4, 1.0, (240, 150, 165, 255))
    if mouth_open: e(hx + 2.5, hy + 7, 1.6, 1.2, (200, 90, 110, 255))
    # outline: dilate the silhouette and put it behind
    alpha = im.split()[3].filter(ImageFilter.MaxFilter(2 * CS + 1))
    outline = Image.new("RGBA", im.size, (80, 56, 48, 255)); outline.putalpha(alpha)
    out = Image.alpha_composite(outline, im)
    return out.resize((FW, FH), Image.LANCZOS)

frames = [
    kitten([(13, 0), (18, -2), (27, -2), (32, 0)]),
    kitten([(14, -2), (17, 0), (28, 0), (31, -2)], tail_up=0.3),
    kitten([(13, 0), (18, -2), (27, -2), (32, 0)], tail_up=0.6),
    kitten([(12, -2), (19, 0), (26, 0), (33, -2)], tail_up=0.3),
    kitten([(10, -6), (20, -6), (26, -6), (36, -6)], tail_up=1.0, ears_up=2, mouth_open=True),   # falling
    kitten([(15, 0), (17, 0), (29, 0), (31, 0)], tail_up=-0.2),                                   # sitting / kneading
]
sheet = Image.new("RGBA", (FW * len(frames), FH), (0, 0, 0, 0))
for i, f in enumerate(frames): sheet.paste(f, (i * FW, 0))
p = os.path.join(OUT, "cat_sheet.bytes"); sheet.save(p, format="PNG")
sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save(os.path.join(os.path.dirname(__file__), "cat_sheet_preview.png"))
print("cat_sheet", sheet.size)
