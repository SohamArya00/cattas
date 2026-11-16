"""Builds Level 1 (The Bath Steps) from Codi's 1960s bathroom picture WITHOUT TOUCHING THE ARTWORK.

  tools/art_sources/bathroom_1960s.webp   Codi's AI placeholder picture (1672x941) - never edited
  Assets/Resources/Art/bathroom_bg.bytes  the SAME picture, losslessly converted to PNG (Unity can't read WebP)
  Assets/Resources/Art/bathroom_collision.bytes  invisible collision map (data, never shown)
  Assets/Resources/Art/bathroom_stone.bytes      invisible "can't be dug" map (data, never shown)
  Assets/Resources/Levels/level01.txt

All shapes below are in SOURCE picture pixels (x right, y DOWN, like an image editor). One level pixel = 2 picture pixels.
Collision colours: red = BACK plane only (steps), green = FRONT plane only (floor-level things), yellow = both."""
import os
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(__file__)
ART = os.path.join(HERE, "..", "Assets", "Resources", "Art")
LEVELS = os.path.join(HERE, "..", "Assets", "Resources", "Levels")
SRC = os.path.join(HERE, "art_sources", "bathroom_1960s.webp")

src = Image.open(SRC).convert("RGBA")
W, H = src.size                       # 1672 x 941
SCALE = 2
GW, GH = 836, 471                     # level size in level pixels (471 = ceil(941 / 2))

# the picture itself: same pixels, PNG container
src.save(os.path.join(ART, "bathroom_bg.bytes"), format="PNG", optimize=True)

def lx(x): return x / SCALE
def ly(y): return (H - y) / SCALE     # picture y (down) -> level y (up)

BACK, FRONT, BOTH = (255, 0, 0, 255), (0, 255, 0, 255), (255, 255, 0, 255)

# ---- invisible collision shapes: (x0, y0, x1, y1, plane colour, stone?, note) ---------------------
FLOOR_TOP = 832                       # where kittens' feet touch the floor (the stool legs meet the floor here)
T1, T2, T3 = 708, 753, 798            # where kittens' feet touch the three bath steps
TH = 16                               # tread thickness (thin ledges: dig one and you drop to the next)
STEP_END = 1075                       # the steps run behind the stool until they meet the vanity
shapes = [
    (0, FLOOR_TOP, W, H,               BOTH,  True,  "floor"),
    (413, T1, STEP_END, T1 + TH,       BACK,  False, "top step"),
    (357, T2, STEP_END, T2 + TH,       BACK,  False, "middle step"),
    (287, T3, STEP_END, T3 + TH,       BACK,  False, "bottom step"),
    (405, T1 - 40, 413, T1,            BACK,  False, "tile wall at the top step's left end"),
    (349, T2 - 40, 357, T2,            BACK,  False, "tile wall at the middle step's left end"),
    (279, T3 - 40, 287, T3,            BACK,  False, "tile wall at the bottom step's left end"),
    (460, 628, 528, T1,                BOTH,  True,  "plant pot (kittens drop off its rim)"),
    (772, 640, 790, T1,                BACK,  False, "towel hanging over the bath"),
    (1070, 625, 1540, FLOOR_TOP,       BOTH,  True,  "vanity unit"),
    (938, 790, 962, FLOOR_TOP + 6,     FRONT, True,  "stool's left front leg"),
    (45, 572, 155, 700,                BOTH,  True,  "toilet cistern"),
    (62, 690, 196, FLOOR_TOP,          BOTH,  True,  "toilet bowl"),
    (196, 690, 228, 722,               BOTH,  True,  "toilet seat"),
    (1550, 710, 1650, FLOOR_TOP,       BOTH,  True,  "laundry basket"),
]

coll = Image.new("RGBA", (W, H + 1), (0, 0, 0, 0))         # +1 row on top so it lines up with the picture exactly
stone = Image.new("RGBA", (W, H + 1), (0, 0, 0, 0))
cd, sd = ImageDraw.Draw(coll), ImageDraw.Draw(stone)
for x0, y0, x1, y1, colour, is_stone, note in shapes:
    box = [x0, y0 + 1, x1 - 1, y1]                           # +1: collision image has one extra row on top
    cd.rectangle(box, fill=colour)
    if is_stone: sd.rectangle(box, fill=(255, 255, 255, 255))
for name, im in (("bathroom_collision", coll), ("bathroom_stone", stone)):
    im.save(os.path.join(ART, name + ".bytes"), format="PNG", optimize=True)

# a review picture (NOT used by the game): collision drawn over the untouched picture
review = src.copy()
ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
ov.paste(coll.crop((0, 1, W, H + 1)), (0, 0))
ov.putalpha(ov.split()[3].point(lambda v: 110 if v else 0))
Image.alpha_composite(review, ov).save(os.path.join(HERE, "bathroom_collision_review.png"))

# ---- the level file ---------------------------------------------------------------------------
hatch_x, hatch_y = lx(494), ly(612)                           # kittens appear above the plant pot and drop onto its rim
goal = (lx(924), ly(738) - 1, lx(1104), ly(600))              # Mum's seat: x 462-552, from the cushion top upwards
mum_x, mum_y = lx(1020), ly(738)
view = (0, int(ly(904)) + 1, GW, GH)                          # camera never shows the strip below the floor
front_y = int(ly(FLOOR_TOP)) + 4

level = f"""// Cattas level 1 - The Bath Steps (1960s bathroom)
// Made by tools/make_bathroom_level.py. The PICTURE IS NEVER CHANGED: all collision is an invisible data picture on top.
// Depth planes: kittens start in the BACK plane (steps). Landing on the floor moves them to the FRONT plane, so they
// walk behind the stool on the steps and in front of the steps on the floor.
name: The Bath Steps
hint: The kittens want Mum! Dig down the steps with Litter Lord, then build a bridge up to her stool.
size: {GW} {GH}
view: {view[0]} {view[1]} {view[2]} {view[3]}
background: bathroom_bg
collision: bathroom_collision
stone: bathroom_stone
frontplane: {front_y}
hatch: {int(hatch_x)} {int(hatch_y)} 1
goal: {int(goal[0])} {int(goal[1])} {int(goal[2])} {int(goal[3])}
mum: {int(mum_x)} {int(ly(738))} -1
cats: 10
need: 6
rate: 2.5
speed: 40
time: 360
par: 180
exit: mother
markers: off
traits: LitterLord=6, HairballBridge=8
tip: start | The kittens drop off the plant pot and get stuck between the pot and the towel. Pick LITTER LORD, then click a kitten to dig down. Dig away from the pot's edge!
tip: below 100 | Down one step - but they're boxed in again. Dig down again!
tip: below 80 | One more step to go. Dig through to the floor!
tip: below 62 | On the floor! Mum's stool is too high to jump. Pick HAIRBALL BRIDGE and click a kitten walking towards the stool.
tip: bridges 1 | Hrrk! That bridge isn't tall enough. Click the builder AGAIN to keep it building up towards Mum!
tip: saved 1 | A kitten made it to Mum! Get the rest home too.
map:
"""
open(os.path.join(LEVELS, "level01.txt"), "w", newline="\n").write(level)
print("bathroom_bg is pixel-identical to the supplied picture:",
      np.array_equal(np.asarray(Image.open(os.path.join(ART, 'bathroom_bg.bytes')).convert('RGBA')), np.asarray(src)))
print("level 1 written. hatch", (int(hatch_x), int(hatch_y)), "goal", tuple(int(v) for v in goal), "mum", (int(mum_x), int(ly(738))),
      "frontplane", front_y, "view", view)
for name, y in (("top step", T1), ("middle step", T2), ("bottom step", T3), ("floor", FLOOR_TOP)):
    print(f"  {name}: feet at level y = {ly(y):.1f}")
