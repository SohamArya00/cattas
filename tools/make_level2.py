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
import math

def ell_poly(cx, cy, a, b, t0=0, t1=360, n=96):
    """points of an ellipse arc (picture px, y down; angle measured with y DOWN: 0=right, 90=bottom)"""
    return [(cx + a * math.cos(math.radians(t0 + (t1 - t0) * i / n)), cy + b * math.sin(math.radians(t0 + (t1 - t0) * i / n))) for i in range(n + 1)]

def R(x0, y0, x1, y1): return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]

# dais tier lips (feet line; measured by eye from 2x crops, +-3px) - the arcs the kittens follow round the steps
DAIS_UP = [(340, 633), (400, 638), (450, 643), (500, 650), (550, 655), (600, 658), (700, 659), (800, 659), (900, 657), (1000, 652), (1075, 648), (1092, 646)]
DAIS_LO = [(283, 677), (330, 682), (400, 690), (450, 693), (550, 699), (650, 702), (750, 703), (850, 703), (950, 701), (1000, 697), (1075, 691), (1090, 689)]
# (polygon, lane letters, stone?, note)    lanes: F front, M mid, B back (several letters = solid in several lanes)
shapes = [
    (R(0, 785, W, H),                  "F", True,  "floor tiles"),
    (R(1080, 690, 1232, 742),          "F", False, "stool cushion (soft)"),
    (ell_poly(1156, 690, 76, 10),      "F", False, "stool cushion top (round)"),
    (R(1085, 737, 1105, 782), "F", True, "stool leg (brass)"), (R(1130, 737, 1142, 777), "F", True, "stool leg"),
    (R(1160, 737, 1182, 782), "F", True, "stool leg"), (R(1208, 737, 1226, 782), "F", True, "stool leg"),
    (DAIS_LO + [(1090, 745), (283, 745)],  "M", True, "dais lower tier (marble) - follows the curved lip"),
    (DAIS_UP + [(1092, 706), (337, 706)],  "B", True, "dais top tier (marble) - sits BEHIND the lower tier, follows the curved lip"),
    ([(590, 546), (935, 546), (925, 590), (900, 620), (870, 634), (660, 634), (630, 620), (603, 590)], "M", True, "tub body (porcelain bowl)"),
    (R(467, 551, 539, 627),            "M", True,  "floor plant pot (ceramic)"),
    (R(38, 497, 157, 613),             "MB", True, "toilet tank (porcelain; the lid's back edge runs into it)"),
    (ell_poly(162, 613, 70, 11),       "M", True,  "toilet seat (round)"),
    ([(90, 626), (233, 626), (230, 645), (215, 670), (200, 690), (198, 745), (90, 745)], "M", True, "toilet bowl (porcelain)"),
    (R(1082, 556, 1540, 575),          "M", True,  "vanity marble counter"),
    (R(1090, 575, 1535, 725),          "M", False, "vanity cabinet (timber)"),
    (R(1485, 725, 1502, 752), "M", True, "vanity leg (brass)"), (R(1515, 725, 1530, 748), "M", True, "vanity leg (brass)"),
    (ell_poly(1289, 500, 96, 53, 0, 180) , "M", True,  "sink bowl (porcelain, round)"),
    (R(677, 438, 745, 512),            "B", True,  "brass taps (tub)"),
    (R(882, 438, 935, 510),            "B", False, "vase on the tub rim (soft)"),
    (R(1262, 437, 1335, 497),          "B", True,  "sink faucet (brass)"),
    (R(1445, 475, 1500, 555),          "B", False, "vase on the vanity (soft)"),
    (R(485, 484, 955, 492),            "B", True,  "window sill"),
    (R(485, 103, 955, 111), "B", True, "window frame top bar"), (R(485, 184, 955, 191), "B", True, "window bar 1 (black metal)"),
    (R(485, 284, 955, 291), "B", True, "window bar 2"), (R(485, 387, 955, 393), "B", True, "window bar 3"),
    (R(0, 398, 280, 404), "B", True, "tile top edge (left of the cat picture)"), (R(410, 398, 482, 404), "B", True, "tile top edge (right of the cat picture)"),
    (R(252, 486, 435, 494),            "B", True,  "brass grab bar"),
    (R(18, 168, 215, 180),             "B", False, "left shelf 1 (timber)"), (R(8, 329, 215, 341), "B", False, "left shelf 2 (timber)"),
    (R(955, 266, 1140, 278),           "B", False, "right gold shelf A"), (R(955, 356, 1140, 368), "B", False, "right gold shelf B"),
    (R(1553, 437, 1652, 447),          "B", False, "niche shelf 1"), (R(1553, 524, 1652, 534), "B", False, "niche shelf 2"),
    (R(1553, 609, 1652, 619),          "B", False, "niche shelf 3"), (R(1553, 694, 1652, 704), "B", False, "niche shelf 4"),
    (R(280, 257, 410, 267),            "B", False, "cat picture frame top (wood)"), (R(1525, 58, 1648, 68), "B", False, "portrait frame top (wood)"),
    (R(1148, 222, 1182, 230),          "B", True,  "left sconce top (brass)"), (R(1457, 207, 1495, 215), "B", True, "right sconce top (brass)"),
    (ell_poly(624.1, 148.75, 83.8, 73.9),  "B", False, "left hanging light globe (soft, diggable)"),
    (ell_poly(1408.1, 124.2, 52.6, 51.4),  "B", False, "right hanging light globe (soft, diggable)"),
]
# brass rings at the bottom of each hanging light are hard (drawn after the globes)
HARD_B = [(R(588, 208, 662, 224), "B", True, "left light bottom brass ring"), (R(1382, 168, 1436, 178), "B", True, "right light bottom brass ring")]
LANE = {"B": 1, "F": 2, "M": 4}
# the tub rim: a thin hard band whose TOP is just under the kittens' feet (so Zoomies etc. don't see it as a wall)
RIM = dict(cx=772.8, cy=523.5, a=207.0, b=10.5)          # walking ellipse (picture px), a bit inside the fitted outer edge
BAND = 6                                                   # band thickness (picture px) = 3 level px
SHIFT = 2                                                  # picture px, measured: band top sits just under the kittens' feet (see tools/make_level2.py history)

solid = np.zeros((H + 1, W), np.uint8); stone_a = np.zeros((H + 1, W), np.uint8)
for poly, lane, is_stone, note in shapes + HARD_B:
    m = Image.new("L", (W, H + 1), 0); ImageDraw.Draw(m).polygon([(x, y + 1) for x, y in poly], fill=255)
    m = np.asarray(m) > 127
    for ch in lane: solid[m] |= LANE[ch]
    if is_stone: stone_a[m] = 255
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
def blk(x0, y0, x1, y1): return (int(lx(x0)), int(ly(y1)), int(lx(x1)) + 1, int(ly(y0)) + 1)
sinkf = blk(1262, 437, 1335, 497)
extra_paths = [   # name cx cy a b closed|open [deg0 deg1] frontLane backLane [slide]   (level px)
    f"path: lid {lx(162):.2f} {ly(611):.2f} 34.00 4.50 closed M B",
    f"path: sink {lx(1289.5):.2f} {ly(499):.2f} 47.00 2.50 closed M B",
    f"path: stool {lx(1156):.2f} {ly(690):.2f} 37.00 4.50 closed F F",
    f"path: lampL {lx(624.1):.2f} {ly(148.75):.2f} 41.40 36.50 open 45 135 B B slide",
    f"path: lampR {lx(1408.1):.2f} {ly(124.2):.2f} 26.00 25.40 open 45 135 B B slide",
]
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
{chr(10).join(extra_paths)}\nrailblock: {sinkf[0]} {sinkf[1]} {sinkf[2]} {sinkf[3]} sink back
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
