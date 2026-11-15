"""Turns Codi's 1960s bathroom picture into Level 1.

Source: tools/art_sources/bathroom_1960s.webp (AI placeholder art, 1672x941).
The bottom strip (garbled AI text) is cropped off -> 1672x904 picture = an 836x452 level at 2x detail.

Writes:
  Assets/Resources/Art/bathroom_bg.bytes     the whole picture, with the hollow insides of the steps shaded
  Assets/Resources/Art/bathroom_fg.bytes     only the solid parts (treads, floor, pot, stool seat, vanity, rubber ducks)
  Assets/Resources/Art/bathroom_stone.bytes  which solid parts can't be dug (floor, pot, seat, vanity, ducks)
  Assets/Resources/Levels/level01.txt

All the shapes below are in SOURCE picture pixels (y measured DOWN, like an image editor)."""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(__file__)
ART = os.path.join(HERE, "..", "Assets", "Resources", "Art")
LEVELS = os.path.join(HERE, "..", "Assets", "Resources", "Levels")
SRC = os.path.join(HERE, "art_sources", "bathroom_1960s.webp")

PIC_H = 904                      # crop the garbled text off the bottom
SCALE = 2                        # picture pixels per level pixel
src = Image.open(SRC).convert("RGBA").crop((0, 0, 1672, PIC_H))
W, H = src.size
GW, GH = W // SCALE, H // SCALE  # level size: 836 x 452

# --- solid shapes (source pixels: x0, y0, x1, y1) -------------------------------------
FLOOR = (0, 820, W, H)
STEP_END = 700                    # the steps' right ends (leaves open floor in front of the stool for bridges)
TREADS = [                        # the three bath steps, as ledges you can dig through
    (413, 699, STEP_END, 713),    # top step
    (357, 745, STEP_END, 759),    # middle step
    (287, 789, STEP_END, 801),    # bottom step
]
POT = (462, 628, 530, 699)        # plant pot on the top step
SEAT = (937, 735, 1103, 790)      # stool cushion (Mum sits here)
VANITY = (1075, 625, W, 785)      # vanity cabinet (on legs - you can walk underneath)
DUCK_W, DUCK_H = 26, 24           # rubber ducks at the right end of each step
DUCKS = [(STEP_END - DUCK_W, t[1] - DUCK_H + 2) for t in TREADS]

def lvl(x, y):
    """source pixel -> level pixel (y up)"""
    return x / SCALE, (H - y) / SCALE

# --- the rubber duck (drawn at 8x then shrunk) -------------------------------------------
def duck_image():
    k = 8
    im = Image.new("RGBA", (DUCK_W * k, DUCK_H * k), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    def e(x0, y0, x1, y1, c): d.ellipse([x0 * k, y0 * k, x1 * k, y1 * k], fill=c)
    e(1, 9, 24, 23, (255, 214, 60, 255))          # body
    e(2, 15, 22, 23, (236, 180, 30, 255))         # belly shade
    e(13, 1, 25, 13, (255, 214, 60, 255))         # head (facing right)
    d.polygon([(23 * k, 7 * k), (26 * k, 8 * k), (23 * k, 10 * k)], fill=(255, 130, 40, 255))   # beak
    e(19, 4.5, 21.5, 7, (40, 30, 30, 255)); e(19.6, 5, 20.4, 5.8, (255, 255, 255, 255))         # eye
    e(4, 12, 12, 18, (255, 228, 110, 255))        # wing highlight
    a = im.split()[3].filter(ImageFilter.MaxFilter(k // 2 * 2 + 1))
    ol = Image.new("RGBA", im.size, (120, 70, 30, 255)); ol.putalpha(a)
    im = Image.alpha_composite(ol, im)
    return im.resize((DUCK_W, DUCK_H), Image.LANCZOS)

duck = duck_image()

# --- foreground: copy the solid parts of the picture -------------------------------------
mask = Image.new("L", (W, H), 0); md = ImageDraw.Draw(mask)
stone = Image.new("L", (W, H), 0); sd = ImageDraw.Draw(stone)
DUCK_RECTS = [(dx, dy, dx + DUCK_W, dy + DUCK_H) for dx, dy in DUCKS]
for r in [FLOOR] + TREADS + [POT, SEAT, VANITY] + DUCK_RECTS:     # ducks are solid blocks (their round backs aren't ramps!)
    md.rectangle([r[0], r[1], r[2] - 1, r[3] - 1], fill=255)
for r in [FLOOR, POT, SEAT, VANITY] + DUCK_RECTS:
    sd.rectangle([r[0], r[1], r[2] - 1, r[3] - 1], fill=255)
fg = Image.new("RGBA", (W, H), (0, 0, 0, 0))
fg.paste(src, (0, 0), mask)
for (dx, dy) in DUCKS:
    fg.alpha_composite(duck, (dx, dy))
    stone.paste(255, (dx, dy, dx + DUCK_W, dy + DUCK_H), duck.split()[3].point(lambda v: 255 if v > 127 else 0))

# --- a second towel hung over the bath above the top step (the kittens drop out of it) ---
TOWEL = (772, 598, 866, 716)                     # the towel already on the bath
towel = src.crop(TOWEL)
tmask = Image.new("L", towel.size, 0); ImageDraw.Draw(tmask).rounded_rectangle([3, 0, towel.width - 4, towel.height - 3], radius=6, fill=255)
TOWEL2_X = 612
src = src.copy()
sh_layer = Image.new("RGBA", src.size, (0, 0, 0, 0))
sh_layer.paste((40, 10, 10, 110), (TOWEL2_X + 4, TOWEL[1] + 5), tmask)
src = Image.alpha_composite(src, sh_layer.filter(ImageFilter.GaussianBlur(4)))     # soft shadow behind the towel
src.paste(towel, (TOWEL2_X, TOWEL[1]), tmask)
fg.paste(src, (0, 0), mask)                      # refresh the solid parts from the updated picture
for (dx, dy) in DUCKS: fg.alpha_composite(duck, (dx, dy))

# --- paw prints on the floor: "start your bridge here" ---------------------------------
pd = ImageDraw.Draw(src)
def paw(cx, cy, r=5):
    col = (255, 246, 220, 255)
    pd.ellipse([cx - r, cy - r * 0.7, cx + r, cy + r * 0.9], fill=col)
    for dx, dy in [(-r * 1.1, -r * 1.3), (-r * 0.35, -r * 1.9), (r * 0.45, -r * 1.9), (r * 1.2, -r * 1.3)]:
        pd.ellipse([cx + dx - r * 0.45, cy + dy - r * 0.5, cx + dx + r * 0.45, cy + dy + r * 0.5], fill=col)
for i, px in enumerate(range(714, 772, 14)):     # along the foot of the steps, just above the floor line
    paw(px, 813 + (2 if i % 2 else -1), r=3.2)

# --- background: the picture, with the hollow spaces under each step shaded ------------
bg = np.asarray(src, np.float32).copy()
cav = Image.new("L", (W, H), 0); cd = ImageDraw.Draw(cav)
gaps = [(TREADS[0][0], TREADS[0][3], STEP_END, TREADS[1][1]),
        (TREADS[1][0], TREADS[1][3], STEP_END, TREADS[2][1]),
        (TREADS[2][0], TREADS[2][3], STEP_END, FLOOR[1])]
for g in gaps: cd.rectangle(g, fill=255)
cav = np.asarray(cav.filter(ImageFilter.GaussianBlur(3)), np.float32) / 255.0
# darker towards the top of each gap (shadow under the tread above)
bg[..., :3] *= (1.0 - 0.55 * cav)[..., None]
bg_img = Image.fromarray(np.clip(bg, 0, 255).astype(np.uint8), "RGBA")
for r in DUCK_RECTS:                 # the solid block behind each duck matches the shaded background exactly
    fg.paste(bg_img.crop(r), (r[0], r[1]))
for (dx, dy) in DUCKS: fg.alpha_composite(duck, (dx, dy))

def save(img, name):
    path = os.path.join(ART, name + ".bytes")
    img.save(path, format="PNG", optimize=True)
    print(name, img.size, os.path.getsize(path) // 1024, "KB")
save(bg_img, "bathroom_bg")
save(fg, "bathroom_fg")
save(Image.merge("RGBA", (stone, stone, stone, stone)), "bathroom_stone")

# preview of what's solid (for checking)
prev = Image.alpha_composite(bg_img, fg)
ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
ov.paste((0, 255, 255, 70), (0, 0), mask)
ov.paste((255, 0, 255, 90), (0, 0), stone)
Image.alpha_composite(prev, ov).save(os.path.join(HERE, "bathroom_solid_preview.png"))

# --- the level file ----------------------------------------------------------------------
CS = 8
rows, cols = (GH + CS - 1) // CS, (GW + CS - 1) // CS
grid = [["."] * cols for _ in range(rows)]
def put(gx, gy, ch):
    c, r = int(gx // CS), rows - 1 - int(gy // CS)
    grid[r][c] = ch
hx, hy = lvl(TOWEL2_X + 45, 650)          # kittens tumble out of the towel hung over the bath
put(hx, hy, "H")
ex, ey = lvl(1000, 735)                    # Mum's spot on the stool
put(ex, ey - 4, "E")
area = (445, 556, 1196, PIC_H)             # Codi's Level 1 crop
ax0, ay1 = lvl(area[0], area[1]); ax1, ay0 = lvl(area[2], area[3])

level = f"""// Cattas level 1 - The Bath Steps (1960s bathroom)
// Made by tools/make_bathroom_level.py from Codi's AI placeholder picture.
// Painted level: the FOREGROUND picture decides what's solid, the STONE picture what can't be dug.
name: The Bath Steps
hint: The kittens want Mum! Dig down the steps with Litter Lord, then build a way up to her.
size: {GW} {GH}
area: {int(ax0)} {int(ay0)} {int(ax1)} {int(ay1)}
cats: 10
need: 6
rate: 2.5
speed: 40
time: 300
par: 150
exit: mother
background: bathroom_bg
foreground: bathroom_fg
stone: bathroom_stone
markers: off
traits: LitterLord=5, HairballBridge=6
tip: start | Kittens on the top step! Pick LITTER LORD, then click a kitten to dig down. (Not right under the towel - new kittens would fall straight through!)
tip: below 85 | Great digging! Dig again to get down to the next step.
tip: below 62 | One more step to go - dig down to the floor!
tip: below 48 | On the floor! Mum's stool is too high to jump. Pick HAIRBALL BRIDGE and click a kitten as it walks over the PAW PRINTS towards Mum.
tip: bridges 1 | Hrrk! One bridge isn't quite high enough. Click the builder AGAIN to keep it building higher, all the way up to Mum!
tip: saved 1 | A kitten made it to Mum! Get the rest home too.
map:
""" + "\n".join("".join(r) for r in grid) + "\n"
open(os.path.join(LEVELS, "level01.txt"), "w", newline="\n").write(level)
print("level01 written: area", (int(ax0), int(ay0), int(ax1), int(ay1)), "hatch", (hx, hy), "exit", (ex, ey))
for name, r in [("top step", TREADS[0]), ("middle step", TREADS[1]), ("bottom step", TREADS[2]), ("floor", FLOOR), ("seat", SEAT)]:
    print(f"  {name}: top at level y={lvl(0, r[1])[1]:.1f}, x {r[0] / SCALE:.0f}-{r[2] / SCALE:.0f}")
