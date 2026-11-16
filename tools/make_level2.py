"""Builds Level 2 (the 1960s bathroom, second picture) as a PHYSICS SANDBOX. The picture is never touched.

  Assets/Resources/Art/bathroom2_bg.bytes         the picture, losslessly converted to PNG
  Assets/Resources/Art/bathroom2_collision.bytes  invisible collision (data)
  Assets/Resources/Art/bathroom2_stone.bytes      invisible "can't be dug" map (data)
  Assets/Resources/Levels/level02.txt

First slice: the bath rim is a closed loop PATH (see docs/LEVEL2_PHYSICS_SPEC.md). Shapes are in PICTURE pixels (y down);
one level pixel = 2 picture pixels."""
import os
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(__file__)
ART = os.path.join(HERE, "..", "Assets", "Resources", "Art")
LEVELS = os.path.join(HERE, "..", "Assets", "Resources", "Levels")
src = Image.open(os.path.join(HERE, "art_sources", "bathroom_level2_1960s.webp")).convert("RGBA")
W, H = src.size
GW, GH = 836, 471
src.save(os.path.join(ART, "bathroom2_bg.bytes"), format="PNG", optimize=True)

def lx(x): return x / 2
def ly(y): return (H - y) / 2

# Depth LANES (layers): F = front (floor, stool), M = mid (dais, tub, toilet, vanity), B = back (wall things, tub back edge).
# A kitten is blocked only by walls in its own lane, but can STAND on a surface from any lane (and moves to that lane).
# (x0, y0, x1, y1, lane, stone?, note)        stone = can't be dug (hard list: marble, porcelain, brass, tile, floor)
shapes = [
    (0, 785, W, H,            "F", True,  "floor tiles (the floor lane is y=785; kittens walk in front of everything)"),
    (1080, 685, 1232, 735,    "F", False, "stool cushion (soft)"),
    (1085, 737, 1105, 782,    "F", True,  "stool leg (brass)"), (1130, 737, 1142, 777, "F", True, "stool leg"),
    (1160, 737, 1182, 782,    "F", True,  "stool leg"), (1208, 737, 1226, 782, "F", True, "stool leg"),
    (283, 706, 1090, 745,     "M", True,  "dais lower tier (marble)"),
    (337, 650, 1092, 706,     "M", True,  "dais top tier (marble)"),
    (590, 546, 935, 650,      "M", True,  "tub body (porcelain)"),
    (467, 551, 539, 627,      "M", True,  "floor plant pot (ceramic)"),
    (38, 497, 155, 615,       "M", True,  "toilet tank (porcelain)"), (65, 598, 232, 748, "M", True, "toilet bowl (porcelain)"),
    (1082, 556, 1540, 575,    "M", True,  "vanity marble counter"),
    (1090, 575, 1535, 725,    "M", False, "vanity cabinet (timber)"),
    (1485, 725, 1502, 752,    "M", True,  "vanity leg (brass)"), (1515, 725, 1530, 748, "M", True, "vanity leg (brass)"),
    (1193, 497, 1385, 553,    "M", True,  "sink bowl (porcelain)"),
    (677, 438, 745, 512,      "B", True,  "brass taps (tub)"),
    (882, 438, 935, 510,      "B", False, "vase on the tub rim (soft)"),
    (1262, 437, 1335, 497,    "B", True,  "sink faucet (brass)"),
    (1445, 475, 1500, 555,    "B", False, "vase on the vanity (soft)"),
    (485, 484, 955, 492,      "B", True,  "window sill"),
    (252, 486, 435, 494,      "B", True,  "brass grab bar"),
    (18, 168, 215, 180,       "B", False, "left shelf 1 (timber)"), (8, 329, 215, 341, "B", False, "left shelf 2 (timber)"),
    (955, 266, 1140, 278,     "B", False, "right gold shelf A"), (955, 356, 1140, 368, "B", False, "right gold shelf B"),
    (1553, 437, 1652, 447,    "B", False, "niche shelf 1"), (1553, 524, 1652, 534, "B", False, "niche shelf 2"),
    (1553, 609, 1652, 619,    "B", False, "niche shelf 3"), (1553, 694, 1652, 704, "B", False, "niche shelf 4"),
    (280, 257, 410, 267,      "B", False, "cat picture frame top (wood)"), (1525, 58, 1648, 68, "B", False, "portrait frame top (wood)"),
    (1148, 222, 1182, 230,    "B", True,  "left sconce top (brass)"), (1457, 207, 1495, 215, "B", True, "right sconce top (brass)"),
]
LANE = {"B": 1, "F": 2, "M": 4}
# the tub rim: a thin hard band whose TOP is just under the kittens' feet (so Zoomies etc. don't see it as a wall)
RIM = dict(cx=772.8, cy=523.5, a=207.0, b=10.5)          # walking ellipse (picture px), a bit inside the fitted outer edge
BAND = 6                                                   # band thickness (picture px) = 3 level px
SHIFT = 2                                                  # picture px, measured: band top sits just under the kittens' feet (see tools/make_level2.py history)

solid = np.zeros((H + 1, W), np.uint8); stone_a = np.zeros((H + 1, W), np.uint8)
for x0, y0, x1, y1, lane, is_stone, note in shapes:
    solid[y0 + 1:y1 + 1, x0:x1] |= LANE[lane]
    if is_stone: stone_a[y0 + 1:y1 + 1, x0:x1] = 255
ring = Image.new("L", (W, H + 1), 0); rd = ImageDraw.Draw(ring)
cy = RIM["cy"] + SHIFT + 1
rd.ellipse([RIM["cx"] - RIM["a"] - BAND / 2, cy - RIM["b"] - BAND / 2, RIM["cx"] + RIM["a"] + BAND / 2, cy + RIM["b"] + BAND / 2], outline=255, width=BAND)
ra = np.asarray(ring) > 127
solid[ra] |= LANE['M'] | LANE['B']; stone_a[ra] = 255          # the rim band: mid + back lanes (front edge = mid, back edge = back)
cr = np.zeros((H + 1, W, 4), np.uint8); cr[..., 0] = np.where(solid & 1, 255, 0); cr[..., 1] = np.where(solid & 2, 255, 0); cr[..., 2] = np.where(solid & 4, 255, 0); cr[..., 3] = np.where(solid > 0, 255, 0)   # red = back, green = front, blue = mid
Image.fromarray(cr, "RGBA").save(os.path.join(ART, "bathroom2_collision.bytes"), format="PNG", optimize=True)
st = np.zeros((H + 1, W, 4), np.uint8); st[..., :3] = 255; st[..., 3] = stone_a
Image.fromarray(st, "RGBA").save(os.path.join(ART, "bathroom2_stone.bytes"), format="PNG", optimize=True)

# review picture (not used by the game)
rev = src.copy(); ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
ov.paste(Image.fromarray(cr, "RGBA").crop((0, 1, W, H + 1)), (0, 0)); ov.putalpha(ov.split()[3].point(lambda v: 110 if v else 0))
Image.alpha_composite(rev, ov).save(os.path.join(HERE, "level2_collision_review.png"))

path = dict(name="tub", cx=lx(RIM["cx"]), cy=ly(RIM["cy"]), a=RIM["a"] / 2, b=RIM["b"] / 2)
taps = (int(lx(677)), int(ly(512)), int(lx(745)) + 1, int(ly(438)) + 1)
vase = (int(lx(882)), int(ly(510)), int(lx(935)) + 1, int(ly(438)) + 1)
level = f"""// Cattas level 2 - Rim Run (PHYSICS SANDBOX on the 1960s bathroom). The picture is never changed.
// Kittens drop onto the tub rim and run round it for ever, unless the brass taps or the vase (back edge) turn them back.
// To get off: Zoomies through the vase, or build a Hairball Bridge off the rim towards the vanity. No story / NG yet.
name: Rim Run (physics test)
hint: Kittens run round the tub rim. The taps and the vase block the back edge. Find a way off!
size: {GW} {GH}
background: bathroom2_bg
collision: bathroom2_collision
lanejump: on
startlane: m
stone: bathroom2_stone
path: tub {path['cx']:.2f} {path['cy']:.2f} {path['a']:.2f} {path['b']:.2f} closed M B
railblock: {taps[0]} {taps[1]} {taps[2]} {taps[3]} tub back
railblock: {vase[0]} {vase[1]} {vase[2]} {vase[3]} tub back
hatch: 484 250 1
goal: 541 192 770 215
cats: 10
need: 3
rate: 2
speed: 50
time: 600
markers: off
traits: LitterLord=10, Fluffchute=10, BiscuitBaker=10, Zoomies=10, CurtainClimber=10, CableChewer=10, Scratcher=10, HairballBridge=10
map:
"""
open(os.path.join(LEVELS, "level02.txt"), "w", newline="\n").write(level)
print("level 2 written")
print("path:", {k: (round(v, 2) if isinstance(v, float) else v) for k, v in path.items()})
print("taps block", taps, "vase block", vase)
