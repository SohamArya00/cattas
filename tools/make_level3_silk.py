"""Builds the Silk Lab data for the Doll House picture (the picture itself is only READ):
  Assets/Resources/Art/level3_bg.bytes      the picture, losslessly as PNG
  Assets/Resources/Art/level3_occl.bytes    mask (alpha) of things kittens walk BEHIND (the bath, the planter) - they get re-drawn on top
  Assets/Resources/Levels/silk_dollhouse.txt   the loop kittens walk round the top dais tier (control points picture px, y down)
Run tools/trace_level3.py first (it writes level3_trace.json)."""
import json, os
import numpy as np
from PIL import Image, ImageDraw
HERE = os.path.dirname(__file__)
ART = os.path.join(HERE, "..", "Assets", "Resources", "Art"); LEVELS = os.path.join(HERE, "..", "Assets", "Resources", "Levels")
src = Image.open(os.path.join(HERE, "art_sources", "bathroom_level3_dollhouse.webp")).convert("RGBA"); W, H = src.size
src.save(os.path.join(ART, "level3_bg.bytes"), format="PNG", optimize=True)

# things in FRONT of the back edge of the tier (polygons in picture px; edges checked on the review picture)
BATH = [(588, 512), (600, 506), (800, 502), (1000, 506), (1033, 513), (1031, 526), (1002, 546), (986, 580), (962, 606), (932, 621), (880, 628),
        (720, 628), (668, 621), (640, 606), (622, 580), (602, 546), (588, 526)]
PLANTER = [(462, 517), (539, 517), (535, 560), (529, 590), (521, 599), (480, 599), (471, 590), (465, 560)]
SS = 4
m = Image.new("L", (W * SS, H * SS), 0); d = ImageDraw.Draw(m)
for poly in (BATH, PLANTER): d.polygon([(x * SS, y * SS) for x, y in poly], fill=255)
m = m.resize((W, H), Image.LANCZOS)
occ = Image.new("RGBA", (W, H), (255, 255, 255, 0)); occ.putalpha(m); occ.save(os.path.join(ART, "level3_occl.bytes"), format="PNG", optimize=True)

tr = json.load(open(os.path.join(HERE, "level3_trace.json")))
front = [p for p in next(r for r in tr["ribbons"] if r["name"] == "dais top tier lip")["points"]][::5]      # every 40 px
pts = [(500, 592, 1.0), (462, 593, 1.0), (432, 595, 0.85), (412, 599, 0.5)]
pts += [(x, y, 0.0) for x, y in front]
pts += [(1113, 613, 0.2), (1101, 601, 0.7), (1078, 593, 1.0), (1030, 591, 1.0), (940, 589.5, 1.0), (850, 588.5, 1.0), (760, 588.5, 1.0),
        (670, 589.5, 1.0), (590, 591, 1.0), (540, 592, 1.0)]
txt = f"""// Silk Lab: the Doll House loop. Kittens walk the top dais tier all the way round: the front lip, round the vanity end, behind the bath, behind the planter.
// control points are picture pixels (y down); z = 0 in front of the bath/planter, 1 behind them. The picture is never changed.
picture level3_bg
size {W} {H}
occluder level3_occl
kitten_scale 3.0
speed 90
depth_squash 0.30
count 10
spawn_gap 1.3
planter 505 520
""" + "\n".join(f"p {x:.2f} {y:.2f} {z:.2f}" for x, y, z in pts) + "\n"
open(os.path.join(LEVELS, "silk_dollhouse.txt"), "w", newline="\n").write(txt)
rev = src.convert("RGB"); ov = Image.new("RGBA", (W, H), (0, 0, 0, 0)); ov.paste((0, 255, 255, 90), mask=m); rev = Image.alpha_composite(rev.convert("RGBA"), ov); rd = ImageDraw.Draw(rev)
rd.line([(x, y) for x, y, z in pts] + [(pts[0][0], pts[0][1])], fill=(255, 255, 0, 255), width=2)
for x, y, z in pts: rd.ellipse([x - 3, y - 3, x + 3, y + 3], fill=(255, 0, 0, 255) if z > 0.5 else (0, 255, 0, 255))
rev.convert("RGB").save(os.path.join(HERE, "level3_silk_review.png"))
rev.convert("RGB").crop((340, 480, 1180, 680)).resize((1680, 400), Image.LANCZOS).save("/tmp/claude-0/-home-user/aa41efe8-b221-55ba-806c-ebb39c188a45/scratchpad/silk_rev_crop.png")
print(len(pts), "control points")
