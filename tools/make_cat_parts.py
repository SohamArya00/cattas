"""Draws the PAPER-DOLL kitten: separate pieces (body, head, tail, legs) that the game animates.
Writes Assets/Resources/Art/kitten_<part>.bytes (PNG data) and Assets/Resources/Art/kitten_rig.txt.

Design coordinates are the same 48x40 grid as the sprite sheet (y measured DOWN, kitten facing right,
feet at the bottom). The rig file converts everything into level pixels for the game.

To make a new skin (dinosaur, dragon...): draw the same pieces with the same joints, save them as
<skin>_body / _head / _tail / _leg_front / _leg_back, and copy the rig file to <skin>_rig.txt."""
import os
from PIL import Image, ImageDraw, ImageChops, ImageFilter
from make_cat_sheet import (U, W, H, layer, ell, poly, gradient_fill, fur, soft,
                            CREAM, CREAM_L, SHADE, POINT, POINT_M, PINK, WHITE, IRIS_L, IRIS_D, PUPIL, OUTLINE)

HERE = os.path.dirname(__file__)
OUT = os.path.join(HERE, "..", "Assets", "Resources", "Art")
PX = 4                      # final image pixels per design unit (same detail as the HD sprite sheet)
WORLD = 15.0 / 48.0         # level pixels per design unit (the kitten is 15 level-pixels wide)
ROOT = (24.0, 39.2)         # design point that sits on the ground (between the paws)
PPU = PX / WORLD            # image pixels per level pixel

def outline(img, skip_top_units=0.0, skip_box=None):
    a = img.split()[3].point(lambda v: 255 if v > 40 else 0).filter(ImageFilter.MaxFilter(U // 2 * 2 + 1))
    a = a.filter(ImageFilter.GaussianBlur(U * 0.12))
    if skip_box is not None:                       # no outline where this piece joins the body
        cut = Image.new("L", a.size, 255); ImageDraw.Draw(cut).rectangle([c * U for c in skip_box], fill=0)
        a = ImageChops.multiply(a, cut)
    ol = Image.new("RGBA", img.size, OUTLINE + (255,)); ol.putalpha(a)
    return Image.alpha_composite(ol, img)

def crop_and_save(img, name, pivot):
    """Crop to the drawing, save it, and return the pivot in final image pixels (from the bottom-left)."""
    bbox = img.getbbox()
    pad = U
    x0, y0, x1, y1 = max(0, bbox[0] - pad), max(0, bbox[1] - pad), min(W, bbox[2] + pad), min(H, bbox[3] + pad)
    # round the crop to whole design-pixels so the scale stays exact
    x0 -= x0 % (U // PX); y0 -= y0 % (U // PX)
    part = img.crop((x0, y0, x1, y1))
    w, h = part.size[0] * PX // U, part.size[1] * PX // U
    part = part.resize((w, h), Image.LANCZOS)
    part.save(os.path.join(OUT, f"kitten_{name}.bytes"), format="PNG", optimize=True)
    part.save(os.path.join(HERE, f"kitten_{name}_preview.png"))
    px = (pivot[0] * U - x0) * PX / U
    py = h - (pivot[1] * U - y0) * PX / U
    return w, h, px, py

def world(p):
    return (p[0] - ROOT[0]) * WORLD, (ROOT[1] - p[1]) * WORLD

# ---------------------------------------------------------------- pieces
def draw_leg(front):
    img = layer(); d = ImageDraw.Draw(img)
    lx = 10.0
    top = CREAM if front else SHADE
    ell(d, lx, 32.5, 3.0, 5.4, top + (255,))
    ell(d, lx + 0.3, 35.2, 3.0, 3.0, POINT_M + (255,))
    ell(d, lx + 0.5, 37.2, 3.3, 2.3, POINT + (255,))
    img = soft(img, 0.05)
    return outline(img, skip_box=(0, 0, 20, 29.5)), (lx, 28.4)

def draw_body():
    img = layer()
    mask = Image.new("L", (W, H), 0); bd = ImageDraw.Draw(mask)
    ell(bd, 22, 26, 13, 9, 255); ell(bd, 20, 22, 10, 5, 255)
    img = Image.alpha_composite(img, gradient_fill(mask, CREAM_L, SHADE))
    img = Image.alpha_composite(img, fur(mask, CREAM, 900, 1.3, [CREAM_L, SHADE, (236, 220, 196)], angle=3.0, seed=2))
    saddle = layer(); ell(ImageDraw.Draw(saddle), 18, 20, 9, 3.5, (196, 168, 140, 110))
    saddle = soft(saddle, 1.5); saddle.putalpha(ImageChops.multiply(saddle.split()[3], mask))
    img = Image.alpha_composite(img, saddle)
    chest = layer(); cd = ImageDraw.Draw(chest)
    ell(cd, 31, 26, 6.2, 6.5, WHITE + (255,)); ell(cd, 29, 30, 5, 4, WHITE + (255,))
    img = Image.alpha_composite(img, chest)
    img = Image.alpha_composite(img, fur(chest.split()[3], WHITE, 300, 1.2, [WHITE, (240, 230, 214)], angle=1.6, seed=3))
    return outline(img), (22.0, 26.0)

def draw_head():
    hx, hy = 34, 15
    img = layer(); ed = ImageDraw.Draw(img)
    poly(ed, [(hx - 9.5, hy - 2), (hx - 7.5, hy - 14.5), (hx - 0.5, hy - 7.5)], POINT + (255,))
    poly(ed, [(hx + 2, hy - 7.5), (hx + 8.5, hy - 14.5), (hx + 10.5, hy - 1.5)], POINT + (255,))
    poly(ed, [(hx - 7.6, hy - 4.5), (hx - 7.0, hy - 11.5), (hx - 3, hy - 7)], PINK + (255,))
    poly(ed, [(hx + 4, hy - 7), (hx + 7.8, hy - 11.5), (hx + 8.6, hy - 4)], (210, 140, 150, 255))
    mask = Image.new("L", (W, H), 0); hd = ImageDraw.Draw(mask)
    ell(hd, hx, hy, 11.2, 10.2, 255); ell(hd, hx - 8.5, hy + 4, 3.5, 3, 255); ell(hd, hx + 9.5, hy + 4, 3.5, 3, 255)
    img = Image.alpha_composite(img, gradient_fill(mask, CREAM_L, SHADE))
    img = Image.alpha_composite(img, fur(mask, CREAM, 500, 0.9, [CREAM_L, SHADE], angle=0.6, seed=4))
    m = layer(); md = ImageDraw.Draw(m)
    ell(md, hx + 1.8, hy + 3.2, 7.2, 5.6, POINT_M + (230,)); ell(md, hx + 2.2, hy + 4.4, 5, 3.8, POINT + (255,))
    m = soft(m, 1.1); m.putalpha(ImageChops.multiply(m.split()[3], mask))
    img = Image.alpha_composite(img, m)
    face = layer(); fd = ImageDraw.Draw(face)
    for ex in (hx - 3.4, hx + 5.4):
        ell(fd, ex, hy - 0.4, 3.3, 3.7, (40, 30, 30, 255)); ell(fd, ex, hy - 0.4, 3.0, 3.4, IRIS_D + (255,))
        ell(fd, ex - 0.2, hy - 0.1, 2.3, 2.7, (96, 170, 232, 255)); ell(fd, ex - 0.3, hy + 0.4, 1.5, 1.6, IRIS_L + (255,))
        ell(fd, ex + 0.3, hy - 0.2, 1.0, 2.5, PUPIL + (255,))
        ell(fd, ex - 1.0, hy - 1.7, 1.0, 1.0, WHITE + (255,)); ell(fd, ex + 1.1, hy + 1.3, 0.45, 0.45, WHITE + (230,))
    poly(fd, [(hx + 1.2, hy + 3.6), (hx + 3.8, hy + 3.6), (hx + 2.5, hy + 5.0)], (232, 140, 156, 255))
    fd.line([((hx + 2.5) * U, (hy + 5.0) * U), ((hx + 2.5) * U, (hy + 5.9) * U)], fill=(60, 40, 36, 255), width=U // 4)
    fd.arc([(hx + 0.9) * U, (hy + 5.0) * U, (hx + 2.6) * U, (hy + 6.6) * U], 20, 160, fill=(60, 40, 36, 255), width=U // 4)
    fd.arc([(hx + 2.4) * U, (hy + 5.0) * U, (hx + 4.1) * U, (hy + 6.6) * U], 20, 160, fill=(60, 40, 36, 255), width=U // 4)
    img = Image.alpha_composite(img, face)
    img = outline(img)
    wk = layer(); wd = ImageDraw.Draw(wk)
    for dy, ang in [(-0.4, -0.12), (0.4, 0.0), (1.2, 0.12)]:
        for side in (-1, 1):
            x0 = hx + 2.5 + side * 2.2; y0 = hy + 5 + dy
            wd.line([(x0 * U, y0 * U), ((x0 + side * 9) * U, (y0 + ang * 9 * side + dy * 0.6) * U)], fill=(255, 255, 255, 190), width=max(2, U // 6))
    img = Image.alpha_composite(img, wk)
    return img, (29.5, 21.5)          # neck joint

def draw_tail():
    img = layer(); td = ImageDraw.Draw(img)
    for i in range(14):
        t = i / 13
        tx = 12 - 8 * t; ty = 24 - 12 * t * t - 2 * t
        r = 3.6 - t * 0.7
        ell(td, tx, ty, r, r, POINT_M + (255,) if t < 0.35 else POINT + (255,))
    img = Image.alpha_composite(img, fur(img.split()[3], POINT, 260, 1.4, [POINT_M, (60, 40, 34), (150, 112, 90)], angle=-2.4, seed=1))
    return outline(img, skip_box=(11, 20, 20, 30)), (12.5, 24.5)   # tail base

# ---------------------------------------------------------------- save pieces + rig
pieces = {}
for name, (img, pivot) in {"body": draw_body(), "head": draw_head(), "tail": draw_tail(),
                           "leg_front": draw_leg(True), "leg_back": draw_leg(False)}.items():
    pieces[name] = crop_and_save(img, name, pivot)
    print(name, pieces[name][:2])

# where each joint sits on the kitten (design coordinates), draw order (0 = furthest back) and shade
joints = [
    ("leg_back_far",   "leg_back",  (13.0, 28.4), 0, 0.85),
    ("leg_front_far",  "leg_front", (27.0, 28.4), 0, 0.85),
    ("tail",           "tail",      (12.5, 24.5), 1, 1.0),
    ("body",           "body",      (22.0, 26.0), 2, 1.0),
    ("leg_back_near",  "leg_back",  (18.0, 28.4), 3, 1.0),
    ("leg_front_near", "leg_front", (32.0, 28.4), 3, 1.0),
    ("head",           "head",      (29.5, 21.5), 4, 1.0),
]
lines = ["// Paper-doll kitten rig (made by tools/make_cat_parts.py)",
         "// ppu = picture pixels per level pixel",
         "// part <name> <picture> <pivotX> <pivotY> <x> <y> <order> <shade>",
         "//   pivot: the joint, in picture pixels from the picture's bottom-left",
         "//   x y:   where that joint sits, in level pixels from the kitten's feet (facing right)",
         f"ppu {PPU:.3f}"]
for name, pic, pos, order, shade in joints:
    w, h, px, py = pieces[pic]
    x, y = world(pos)
    lines.append(f"part {name} kitten_{pic} {px:.1f} {py:.1f} {x:.3f} {y:.3f} {order} {shade}")
open(os.path.join(OUT, "kitten_rig.txt"), "w", newline="\n").write("\n".join(lines) + "\n")
print("\n".join(lines))

# preview: assemble the pieces in the rest pose
prev = Image.new("RGBA", (W * PX // U, H * PX // U), (250, 240, 226, 255))
for name, pic, pos, order, shade in sorted(joints, key=lambda j: j[3]):
    w, h, px, py = pieces[pic]
    im = Image.open(os.path.join(OUT, f"kitten_{pic}.bytes")).convert("RGBA")
    if shade < 1: im = Image.composite(Image.eval(im, lambda v: int(v * shade)).convert("RGBA"), im, im.split()[3]); im.putalpha(Image.open(os.path.join(OUT, f"kitten_{pic}.bytes")).split()[3])
    jx, jy = pos[0] * PX, pos[1] * PX
    prev.alpha_composite(im, (int(round(jx - px)), int(round(jy - (h - py)))))
prev.resize((prev.width * 3, prev.height * 3), Image.LANCZOS).save(os.path.join(HERE, "kitten_rig_preview.png"))
