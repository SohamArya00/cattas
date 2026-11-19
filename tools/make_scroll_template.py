"""Builds the THREE-SCREEN construction template + test level data (placeholder art, drawn by code, not real art).

Three screens, each 1920x1080, joined into one strip you scroll left to right:
   screen 1 LEFT   wall drawn at about 45 degrees: biggest at the far left, shrinking towards the corner (right edge)
   screen 2 MIDDLE flat, head-on
   screen 3 RIGHT  mirror of screen 1: grows from the corner (left edge) to the far right
The wall/floor line is a straight line through a vanishing point on the horizon, so a kitten walking it gets smaller going
towards the corner and bigger going away from it. The same numbers drive the game (kitten size + speed) and are the brief for the artist.
Writes Assets/Resources/Art/scroll_a|b|c.bytes, Assets/Resources/Levels/scroll_test.txt and tools/scroll_template_overview.png"""
import os, math
from PIL import Image, ImageDraw, ImageFont
HERE = os.path.dirname(__file__)
ART = os.path.join(HERE, "..", "Assets", "Resources", "Art"); LEVELS = os.path.join(HERE, "..", "Assets", "Resources", "Levels")
W, H = 1920, 1080
K = 0.55            # how much bigger things get across one side screen (1.0 at the corner -> 1.55 at the far edge)
FORE = 0.72         # a wall at ~45 degrees is squashed sideways to this
HOR = 420           # horizon (eye level), y from the top
FLOOR0, CEIL0 = 780, 90      # wall base and ceiling line in the middle screen
TILE = 45           # wall tile size in the middle screen (px)
KITTEN = 41         # kitten height in the middle screen (px)

def s_at(xg):
    if xg < W: return 1 + K * (W - xg) / W
    if xg <= 2 * W: return 1.0
    return 1 + K * (xg - 2 * W) / W
def f_at(xg):       # sideways squash: 0.72 on the side walls, 1 on the back wall, blended across ~80px at each corner
    d = min(max(abs(xg - W), 0), 1e9); e = abs(xg - 2 * W)
    a = min(1.0, d / 80) if xg < 1.5 * W else min(1.0, e / 80)
    return FORE + (1 - FORE) * (1 - a)
def yfloor(xg): return HOR + (FLOOR0 - HOR) * s_at(xg)
def yceil(xg): return HOR + (CEIL0 - HOR) * s_at(xg)
VP = {0: 2 * W + (W / K) - W, 1: 1.5 * W, 2: W - (W / K) + W}      # vanishing point x (global) per screen
VP[0] = W + W / K; VP[2] = 2 * W - W / K

def tile_columns(screen):
    """x (global) of vertical tile lines, equal world spacing foreshortened + scaled by s"""
    xs = []
    if screen == 1:
        x = W
        while x <= 2 * W + 1: xs.append(x); x += TILE
        return xs
    if screen == 0:
        x = W; out = []
        while x > -50: out.append(x); x -= TILE * FORE * s_at(x)
        return out
    x = 2 * W; out = []
    while x < 3 * W + 50: out.append(x); x += TILE * FORE * s_at(x)
    return out

def font(n):
    try: return ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", n)
    except Exception: return ImageFont.load_default()

names = ["LEFT screen (45 deg)", "MIDDLE screen (flat)", "RIGHT screen (45 deg)"]
files = ["scroll_a", "scroll_b", "scroll_c"]
for sc in range(3):
    img = Image.new("RGB", (W, H), (251, 226, 204)); d = ImageDraw.Draw(img); X0 = sc * W
    # ceiling + wall + floor polygons
    xs = list(range(0, W + 1, 8))
    d.polygon([(x, yceil(X0 + x)) for x in xs] + [(x, yfloor(X0 + x)) for x in reversed(xs)], fill=(250, 208, 186))                  # wall
    d.polygon([(x, yfloor(X0 + x)) for x in xs] + [(W, H), (0, H)], fill=(244, 130, 110))                                          # floor
    d.polygon([(x, yfloor(X0 + x)) for x in xs] + [(x, yfloor(X0 + x) - 0.50 * (FLOOR0 - CEIL0) * s_at(X0 + x)) for x in reversed(xs)], fill=(236, 110, 130))  # pink wainscot
    d.polygon([(x, 0) for x in xs] + [(x, yceil(X0 + x)) for x in reversed(xs)], fill=(255, 196, 170))                              # ceiling soffit
    cols = tile_columns(sc)
    for xg in cols:                                   # vertical tile lines (wainscot)
        x = xg - X0
        if -2 <= x <= W + 2: d.line([(x, yfloor(xg) - 0.50 * (FLOOR0 - CEIL0) * s_at(xg)), (x, yfloor(xg))], fill=(255, 160, 175), width=1)
    for r in range(0, 8):                             # horizontal tile rows follow the wall lines
        pts = [(x, yfloor(X0 + x) - r * TILE * 0.80 * s_at(X0 + x)) for x in xs]
        if r * TILE * 0.80 <= 0.50 * (FLOOR0 - CEIL0) + 1: d.line(pts, fill=(255, 160, 175), width=1)
    # floor rays towards the vanishing point
    vx = VP[sc]
    for xg in cols[::2]:
        yf = yfloor(xg)
        if sc == 1: xb = vx + (xg - vx) * (H - HOR) / (FLOOR0 - HOR)
        else: xb = vx + (xg - vx) * (H - HOR) / max(1.0, yf - HOR)
        d.line([(xg - X0, yf), (xb - X0, H)], fill=(255, 175, 150), width=1)
    if sc == 1:
        for i in range(1, 9):                          # floor rows (receding)
            y = FLOOR0 + (H - FLOOR0) * (i / 8) ** 1.5
            d.line([(0, y), (W, y)], fill=(255, 175, 150), width=1)
    # floor/wall line, horizon, ceiling line
    d.line([(x, yfloor(X0 + x)) for x in xs], fill=(90, 20, 40), width=4)
    d.line([(x, yceil(X0 + x)) for x in xs], fill=(90, 20, 40), width=3)
    d.line([(0, HOR), (W, HOR)], fill=(60, 120, 220), width=2)
    d.text((10, HOR - 26), "horizon (eye level)", fill=(60, 120, 220), font=font(20))
    # kitten height guides
    for gx in range(120, W, 400):
        xg = X0 + gx; s = s_at(xg); yb = yfloor(xg) + 26 * s; h = KITTEN * s; w = h * 1.2
        d.rounded_rectangle([gx - w / 2, yb - h, gx + w / 2, yb], radius=int(h * 0.3), outline=(30, 30, 30), width=2, fill=(255, 255, 255))
        d.text((gx - w / 2, yb + 4), f"kitten {int(h)}px", fill=(30, 30, 30), font=font(18))
    # seams
    for side in (0, W - 1):
        d.line([(side, 0), (side, H)], fill=(0, 160, 90), width=6)
    d.text((W // 2 - 300, 28), f"{sc + 1}. {names[sc]}   scale {s_at(X0):.2f} -> {s_at(X0 + W):.2f}", fill=(40, 40, 40), font=font(34))
    d.text((12, H - 40), "placeholder construction template - green edges must match the neighbouring screen exactly", fill=(40, 40, 40), font=font(22))
    img.save(os.path.join(ART, files[sc] + ".bytes"), format="PNG", optimize=True)
    img.save(os.path.join(HERE, f"scroll_template_{sc + 1}.png"))
# route (kitten floor lane) every 40 px: x, y, scale, foreshortening
pts = []
for xg in range(40, 3 * W - 39, 40):
    s = s_at(xg); pts.append((xg, yfloor(xg) + 26 * s, s, f_at(xg)))
txt = f"""// Scroll Lab: three screens joined into one strip (placeholder construction template, NOT real art)
screens {W} {H} scroll_a scroll_b scroll_c
kitten {KITTEN / 12.0:.3f}
speed 95
count 8
spawn_gap 1.5
""" + "\n".join(f"r {x:.1f} {y:.2f} {s:.4f} {f:.3f}" for x, y, s, f in pts) + "\n"
open(os.path.join(LEVELS, "scroll_test.txt"), "w", newline="\n").write(txt)
ov = Image.new("RGB", (3 * W, H)); [ov.paste(Image.open(os.path.join(HERE, f"scroll_template_{i + 1}.png")), (i * W, 0)) for i in range(3)]
ov.resize((1920, 360), Image.LANCZOS).save(os.path.join(HERE, "scroll_template_overview.png"))
print(len(pts), "route points; floor line y at left edge / corner / middle:", round(yfloor(0)), round(yfloor(W)), round(yfloor(2 * W - 1)))
