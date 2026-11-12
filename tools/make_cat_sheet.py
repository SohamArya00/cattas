"""Draws the HD kitten sprite sheet (based on Codi's seal-point cat).
6 frames side by side, each 192x160: 0-3 walk cycle, 4 falling, 5 sitting.
Saved as Assets/Resources/Art/cat_sheet.bytes (PNG data). The game works out the frame size from the image height.
Drawing is done in "design units" (a 48x40 grid) at 16x supersampling, then shrunk for smooth edges."""
import os, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops

HERE = os.path.dirname(__file__)
OUT = os.path.join(HERE, "..", "Assets", "Resources", "Art")
FW, FH = 192, 160          # final frame size
U = 16                     # supersampled pixels per design unit (48x40 units -> 768x640)
W, H = 48 * U, 40 * U

CREAM = (247, 236, 218); CREAM_L = (255, 250, 240); SHADE = (222, 200, 172)
POINT = (84, 56, 46); POINT_M = (128, 92, 72); PINK = (240, 160, 172); WHITE = (255, 253, 248)
IRIS_L = (150, 214, 250); IRIS_D = (46, 118, 196); PUPIL = (22, 24, 34); OUTLINE = (70, 48, 40)

def layer(): return Image.new("RGBA", (W, H), (0, 0, 0, 0))
def ell(d, cx, cy, rx, ry, fill): d.ellipse([(cx - rx) * U, (cy - ry) * U, (cx + rx) * U, (cy + ry) * U], fill=fill)
def poly(d, pts, fill): d.polygon([(x * U, y * U) for x, y in pts], fill=fill)

def gradient_fill(mask, top, bottom, light=(0.35, 0.25)):
    """Fill a mask with a soft top-left-lit gradient."""
    a = np.array(mask, dtype=np.float32) / 255.0
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    t = np.clip((ys / H) * 0.8 + (xs / W) * 0.2, 0, 1)
    rgb = np.zeros((H, W, 3), np.float32)
    for i in range(3): rgb[..., i] = top[i] * (1 - t) + bottom[i] * t
    out = np.dstack([rgb, a * 255]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")

def fur(mask, base, count, length, colours, angle=0.0, seed=1):
    """Lots of short soft strokes inside a mask - looks like fluffy fur."""
    rnd = random.Random(seed)
    lay = layer(); d = ImageDraw.Draw(lay)
    m = np.array(mask)
    ys, xs = np.nonzero(m > 128)
    if len(xs) == 0: return lay
    for _ in range(count):
        i = rnd.randrange(len(xs)); x, y = xs[i], ys[i]
        a = angle + rnd.uniform(-0.5, 0.5); L = length * U * rnd.uniform(0.5, 1.2)
        col = rnd.choice(colours)
        d.line([(x, y), (x + math.cos(a) * L, y + math.sin(a) * L)], fill=col + (rnd.randint(40, 95),), width=max(2, U // 4))
    lay = lay.filter(ImageFilter.GaussianBlur(U * 0.12))
    lay.putalpha(ImageChops.multiply(lay.split()[3], mask))
    return lay

def soft(img, r): return img.filter(ImageFilter.GaussianBlur(r * U))

def kitten(legs, tail_up=0.0, ears_up=0.0, mouth_open=False, sit=False, seed=0):
    img = layer()
    # ---- tail (fluffy, dark point colour)
    tail = layer(); td = ImageDraw.Draw(tail)
    for i in range(14):
        t = i / 13
        tx = 12 - 8 * t; ty = 24 - (10 + 6 * tail_up) * t * t - 2 * t
        r = 3.6 - t * 0.7
        ell(td, tx, ty, r, r, POINT_M + (255,) if t < 0.35 else POINT + (255,))
    tail_mask = tail.split()[3]
    img = Image.alpha_composite(img, tail)
    img = Image.alpha_composite(img, fur(tail_mask, POINT, 260, 1.4, [POINT_M, (60, 40, 34), (150, 112, 90)], angle=-2.4, seed=seed + 1))

    # ---- legs (back pair behind the body)
    def leg(lx, ly, front):
        lay = layer(); d = ImageDraw.Draw(lay)
        top = CREAM if front else SHADE
        ell(d, lx, 32.5 + ly * 0.3, 3.0, 5.2, top + (255,))
        ell(d, lx + 0.3, 35.2 + ly * 0.25, 3.0, 3.0, POINT_M + (255,))
        ell(d, lx + 0.5, 37.2 + ly * 0.2, 3.3, 2.3, POINT + (255,))
        return lay
    for lx, ly in legs[:2]: img = Image.alpha_composite(img, leg(lx, ly, False))

    # ---- body
    body_mask = Image.new("L", (W, H), 0); bd = ImageDraw.Draw(body_mask)
    if sit:
        ell(bd, 20, 27, 12, 9.5, 255); ell(bd, 17, 30, 10, 7, 255)
    else:
        ell(bd, 22, 26, 13, 9, 255); ell(bd, 20, 22, 10, 5, 255)
    body = gradient_fill(body_mask, CREAM_L, SHADE)
    img = Image.alpha_composite(img, body)
    img = Image.alpha_composite(img, fur(body_mask, CREAM, 900, 1.3, [CREAM_L, SHADE, (236, 220, 196)], angle=3.0, seed=seed + 2))
    # darker saddle along the back (seal points shade the back slightly)
    saddle = layer(); ImageDraw.Draw(saddle); ell(ImageDraw.Draw(saddle), 18, 20, 9, 3.5, (196, 168, 140, 110))
    saddle = soft(saddle, 1.5); saddle.putalpha(ImageChops.multiply(saddle.split()[3], body_mask))
    img = Image.alpha_composite(img, saddle)

    for lx, ly in legs[2:]: img = Image.alpha_composite(img, leg(lx, ly, True))

    # fluffy white chest ruff
    chest = layer(); cd = ImageDraw.Draw(chest)
    ell(cd, 31, 26, 6.2, 6.5, WHITE + (255,)); ell(cd, 29, 30, 5, 4, WHITE + (255,))
    chest_mask = chest.split()[3]
    img = Image.alpha_composite(img, chest)
    img = Image.alpha_composite(img, fur(chest_mask, WHITE, 300, 1.2, [WHITE, (240, 230, 214)], angle=1.6, seed=seed + 3))

    # ---- head
    hx, hy = 34, 15
    ears = layer(); ed = ImageDraw.Draw(ears)
    poly(ed, [(hx - 9.5, hy - 2), (hx - 7.5, hy - 14.5 - ears_up), (hx - 0.5, hy - 7.5)], POINT + (255,))
    poly(ed, [(hx + 2, hy - 7.5), (hx + 8.5, hy - 14.5 - ears_up), (hx + 10.5, hy - 1.5)], POINT + (255,))
    poly(ed, [(hx - 7.6, hy - 4.5), (hx - 7.0, hy - 11.5 - ears_up), (hx - 3, hy - 7)], PINK + (255,))
    poly(ed, [(hx + 4, hy - 7), (hx + 7.8, hy - 11.5 - ears_up), (hx + 8.6, hy - 4)], (210, 140, 150, 255))
    img = Image.alpha_composite(img, ears)
    head_mask = Image.new("L", (W, H), 0); hd = ImageDraw.Draw(head_mask)
    ell(hd, hx, hy, 11.2, 10.2, 255); ell(hd, hx - 8.5, hy + 4, 3.5, 3, 255); ell(hd, hx + 9.5, hy + 4, 3.5, 3, 255)  # cheek fluff
    head = gradient_fill(head_mask, CREAM_L, SHADE)
    img = Image.alpha_composite(img, head)
    img = Image.alpha_composite(img, fur(head_mask, CREAM, 500, 0.9, [CREAM_L, SHADE], angle=0.6, seed=seed + 4))
    # seal-point face mask: soft dark patch around the nose and eyes
    mask_l = layer(); md = ImageDraw.Draw(mask_l)
    ell(md, hx + 1.8, hy + 3.2, 7.2, 5.6, POINT_M + (230,)); ell(md, hx + 2.2, hy + 4.4, 5, 3.8, POINT + (255,))
    mask_l = soft(mask_l, 1.1); mask_l.putalpha(ImageChops.multiply(mask_l.split()[3], head_mask))
    img = Image.alpha_composite(img, mask_l)

    # ---- eyes: big, glossy, blue
    face = layer(); fd = ImageDraw.Draw(face)
    for ex in (hx - 3.4, hx + 5.4):
        ell(fd, ex, hy - 0.4, 3.3, 3.7, (40, 30, 30, 255))                      # eye rim
        ell(fd, ex, hy - 0.4, 3.0, 3.4, IRIS_D + (255,))
        ell(fd, ex - 0.2, hy - 0.1, 2.3, 2.7, (96, 170, 232, 255))
        ell(fd, ex - 0.3, hy + 0.4, 1.5, 1.6, IRIS_L + (255,))
        ell(fd, ex + 0.3, hy - 0.2, 1.0, 2.5, PUPIL + (255,))
        ell(fd, ex - 1.0, hy - 1.7, 1.0, 1.0, WHITE + (255,))
        ell(fd, ex + 1.1, hy + 1.3, 0.45, 0.45, WHITE + (230,))
    # nose + mouth
    poly(fd, [(hx + 1.2, hy + 3.6), (hx + 3.8, hy + 3.6), (hx + 2.5, hy + 5.0)], (232, 140, 156, 255))
    fd.line([((hx + 2.5) * U, (hy + 5.0) * U), ((hx + 2.5) * U, (hy + 5.9) * U)], fill=(60, 40, 36, 255), width=U // 4)
    fd.arc([(hx + 0.9) * U, (hy + 5.0) * U, (hx + 2.6) * U, (hy + 6.6) * U], 20, 160, fill=(60, 40, 36, 255), width=U // 4)
    fd.arc([(hx + 2.4) * U, (hy + 5.0) * U, (hx + 4.1) * U, (hy + 6.6) * U], 20, 160, fill=(60, 40, 36, 255), width=U // 4)
    if mouth_open: ell(fd, hx + 2.5, hy + 7.0, 1.4, 1.1, (190, 80, 100, 255))
    img = Image.alpha_composite(img, face)
    # whiskers
    wk = layer(); wd = ImageDraw.Draw(wk)
    for dy, ang in [(-0.4, -0.12), (0.4, 0.0), (1.2, 0.12)]:
        for side in (-1, 1):
            x0 = hx + 2.5 + side * 2.2; y0 = hy + 5 + dy
            wd.line([(x0 * U, y0 * U), ((x0 + side * 9) * U, (y0 + ang * 9 * side + dy * 0.6) * U)], fill=(255, 255, 255, 190), width=max(2, U // 6))

    # ---- soft outline so the kitten reads well against busy backgrounds
    alpha = img.split()[3].point(lambda v: 255 if v > 40 else 0).filter(ImageFilter.MaxFilter(U // 2 * 2 + 1))
    alpha = alpha.filter(ImageFilter.GaussianBlur(U * 0.12))
    outline = Image.new("RGBA", img.size, OUTLINE + (255,)); outline.putalpha(alpha)
    img = Image.alpha_composite(outline, img)
    img = Image.alpha_composite(img, wk)   # whiskers on top, not outlined
    return img.resize((FW, FH), Image.LANCZOS)

if __name__ == "__main__":
    frames = [
        kitten([(13, 0), (18, -2), (27, -2), (32, 0)], seed=10),
        kitten([(14, -2), (17, 0), (28, 0), (31, -2)], tail_up=0.3, seed=20),
        kitten([(13, 0), (18, -2), (27, -2), (32, 0)], tail_up=0.6, seed=30),
        kitten([(12, -2), (19, 0), (26, 0), (33, -2)], tail_up=0.3, seed=40),
        kitten([(10, -6), (20, -6), (26, -6), (36, -6)], tail_up=1.0, ears_up=2, mouth_open=True, seed=50),   # falling
        kitten([(15, 0), (17, 0), (29, 0), (31, 0)], tail_up=-0.2, sit=True, seed=60),                          # sitting / kneading
    ]
    sheet = Image.new("RGBA", (FW * len(frames), FH), (0, 0, 0, 0))
    for i, f in enumerate(frames): sheet.paste(f, (i * FW, 0))
    sheet.save(os.path.join(OUT, "cat_sheet.bytes"), format="PNG", optimize=True)
    sheet.save(os.path.join(HERE, "cat_sheet_preview.png"))
    print("cat_sheet", sheet.size, os.path.getsize(os.path.join(OUT, "cat_sheet.bytes")) // 1024, "KB")
