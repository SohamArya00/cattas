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

# (x0, y0, x1, y1, stone?, note) - planes: 'B' = back plane only (things at the back of the rim), otherwise both planes
shapes = [
    (0, 785, W, H,          True,  "floor (kittens walk in front of everything at y=785)"),
    (283, 706, 1090, 745,   True,  "dais lower tier (marble)"),
    (337, 650, 1092, 706,   True,  "dais top tier (marble)"),
    (590, 546, 935, 650,    True,  "tub body (porcelain)"),
    (677, 438, 745, 512,    True,  "brass taps (back plane)"),
    (882, 438, 935, 510,    False, "vase on the rim (soft, back plane)"),
    (1082, 556, 1540, 575,  True,  "vanity marble counter"),
    (1090, 575, 1535, 725,  False, "vanity cabinet (timber, soft)"),
]
# the tub rim: a thin hard band whose TOP is just under the kittens' feet (so Zoomies etc. don't see it as a wall)
RIM = dict(cx=772.8, cy=523.5, a=207.0, b=10.5)          # walking ellipse (picture px), a bit inside the fitted outer edge
BAND = 6                                                   # band thickness (picture px) = 3 level px
SHIFT = 2                                                  # picture px, measured: band top sits just under the kittens' feet (see tools/make_level2.py history)

solid = np.zeros((H + 1, W), np.uint8); stone_a = np.zeros((H + 1, W), np.uint8)
for x0, y0, x1, y1, is_stone, note in shapes:
    solid[y0 + 1:y1 + 1, x0:x1] = 1 if "back plane" in note else 3
    if is_stone: stone_a[y0 + 1:y1 + 1, x0:x1] = 255
ring = Image.new("L", (W, H + 1), 0); rd = ImageDraw.Draw(ring)
cy = RIM["cy"] + SHIFT + 1
rd.ellipse([RIM["cx"] - RIM["a"] - BAND / 2, cy - RIM["b"] - BAND / 2, RIM["cx"] + RIM["a"] + BAND / 2, cy + RIM["b"] + BAND / 2], outline=255, width=BAND)
ra = np.asarray(ring) > 127
solid[ra] = 3; stone_a[ra] = 255
cr = np.zeros((H + 1, W, 4), np.uint8); cr[..., 0] = np.where(solid & 1, 255, 0); cr[..., 1] = np.where(solid & 2, 255, 0); cr[..., 3] = np.where(solid > 0, 255, 0)
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
stone: bathroom2_stone
path: tub {path['cx']:.2f} {path['cy']:.2f} {path['a']:.2f} {path['b']:.2f} closed
railblock: {taps[0]} {taps[1]} {taps[2]} {taps[3]} tub back
railblock: {vase[0]} {vase[1]} {vase[2]} {vase[3]} tub back
hatch: 300 250 1
goal: 541 192 770 270
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
