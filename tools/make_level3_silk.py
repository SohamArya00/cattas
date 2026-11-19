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
# BACK LINE = the black line where the tier floor meets the wall. Measured to sub-pixel by snapping to the darkest thin row (see
# tools/snap_wall_line.py output in the notes); it is NOT a smooth curve: the wall steps back at x~440 and has an inside corner at x~570.
# Behind the bath (x 640-975) it can't be seen, so it runs straight between the visible ends. Kittens walk FEET_OFF px in front of it.
FEET_OFF = 2.5
def yA(x): return 596.6 - 0.0528 * (x - 400)          # left wall piece, x 400-436 (rises to the right)
def yB(x): return 587.6 - 0.0546 * (x - 442)          # next piece, x 442-570 (planter hides 460-540)
def yR(x): return 612.0 + 0.1029 * (x - 1016)         # right wall piece, x 1016-1120 (comes forward to the right)
meas_C = [(577, 581.0), (585, 581.8), (593, 582.6), (601, 583.5), (609, 584.0), (617, 584.8), (625, 585.6), (633, 586.3)]
def yD(x): return 586.3 + (606.7 - 586.3) * (x - 633) / (975 - 633)     # hidden behind the bath: straight line between the visible ends
back_left = [(x, yB(x), 1.0) for x in (500, 480, 460, 448)] + [(441.5, 587.9, 1.0), (438.5, 593.5, 1.0), (435, yA(435), 1.0)] + [(x, yA(x), 0.9) for x in (425, 412, 402)]
back_right = [(x, yR(x), 1.0) for x in (1116, 1100, 1080, 1060, 1040, 1022)] + [(1013, 610.0, 1.0), (1008, 607.5, 1.0), (995, 607.0, 1.0), (975, 606.7, 1.0)]
back_right += [(x, yD(x), 1.0) for x in (915, 855, 795, 735, 675, 645)] + [(x, y, 1.0) for x, y in reversed(meas_C)]
back_right += [(569, 580.8, 1.0), (561, 581.0, 1.0), (553, 581.2, 1.0), (545, 581.7, 1.0), (528, yB(528), 1.0)]
back_left = [(x, y + FEET_OFF, z) for x, y, z in back_left]; back_right = [(x, y + FEET_OFF, z) for x, y, z in back_right]
pts = back_left + [(399.5, 600.5, 0.4)] + [(x, y, 0.0) for x, y in front] + [(1120, 624.0, 0.2)] + back_right
TUNNEL = [(1124, 624), (1170, 629), (1218, 634), (1275, 640), (1331, 646), (1370, 645), (1402, 645), (1436, 649), (1466, 653), (1505, 651), (1545, 648)]
HOLE = (1545, 648)
FLOOR = [(1650, 781), (1600, 769), (1560, 757), (1500, 758), (1450, 762), (1300, 766), (1200, 765), (1100, 760), (900, 754), (700, 752), (560, 751)]
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
zoom_speed 190
dive 1121 624
""" + "\n".join(f"p {x:.2f} {y:.2f} {z:.2f}" for x, y, z in pts) + "\n"
txt += "// ZOOMIES TUNNEL through the vanity: follows Codi's purple line along the drawers, then out of the hole in the end panel (picture px)\n"
txt += "\n".join(f"t {x:.2f} {y:.2f} 0" for x, y in TUNNEL) + "\n"
txt += "// FLOOR in front of the vanity, right to left (kittens that drop out of the hole walk it and turn round at the ends)\n"
txt += "\n".join(f"f {x:.2f} {y:.2f} 0" for x, y in FLOOR) + "\n"
txt += f"hole {HOLE[0]} {HOLE[1]}\n"
open(os.path.join(LEVELS, "silk_dollhouse.txt"), "w", newline="\n").write(txt)
rev = src.convert("RGB"); ov = Image.new("RGBA", (W, H), (0, 0, 0, 0)); ov.paste((0, 255, 255, 90), mask=m); rev = Image.alpha_composite(rev.convert("RGBA"), ov); rd = ImageDraw.Draw(rev)
rd.line([(x, y) for x, y, z in pts] + [(pts[0][0], pts[0][1])], fill=(255, 255, 0, 255), width=2)
rd.line(TUNNEL, fill=(255, 0, 255, 255), width=3); rd.ellipse([HOLE[0] - 10, HOLE[1] - 10, HOLE[0] + 10, HOLE[1] + 10], outline=(255, 0, 255, 255), width=3); rd.line(FLOOR, fill=(0, 200, 255, 255), width=2)
for x, y, z in pts: rd.ellipse([x - 3, y - 3, x + 3, y + 3], fill=(255, 0, 0, 255) if z > 0.5 else (0, 255, 0, 255))
rev.convert("RGB").save(os.path.join(HERE, "level3_silk_review.png"))
rev.convert("RGB").crop((1060, 520, 1672, 830)).resize((1224, 620), Image.LANCZOS).save("/tmp/claude-0/-home-user/aa41efe8-b221-55ba-806c-ebb39c188a45/scratchpad/silk_rev_crop.png")
print(len(pts), "control points")
