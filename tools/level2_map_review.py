"""Draws a REVIEW map of Level 2 (the 1960s bathroom, second version) on top of Codi's untouched picture.
Nothing here is used by the game: it is a proposal for discussion. Coordinates are picture pixels (y down).
Green = soft (diggable) | Blue = hard (can't dig) | Orange = unsure (needs Codi's call) | Purple = decoration, not solid
Yellow lines = walkable surfaces (numbered) | Cyan line = the floor lane"""
import os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(__file__)
src = Image.open(os.path.join(HERE, "art_sources", "bathroom_level2_1960s.webp")).convert("RGBA")
W, H = src.size
S = 2                                     # draw at 2x so labels are readable
base = src.resize((W * S, H * S), Image.LANCZOS)
ov = Image.new("RGBA", base.size, (0, 0, 0, 0)); d = ImageDraw.Draw(ov)
try: font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 22)
except Exception: font = ImageFont.load_default()

SOFT, HARD, MAYBE, DECOR = (40, 230, 90, 125), (10, 40, 255, 150), (255, 170, 0, 140), (255, 255, 255, 55)
def R(x0, y0, x1, y1, col): d.rectangle([x0 * S, y0 * S, x1 * S, y1 * S], fill=col, outline=col[:3] + (230,), width=2)
def P(pts, col): d.polygon([(x * S, y * S) for x, y in pts], fill=col, outline=col[:3] + (230,))
def E(x0, y0, x1, y1, col): d.ellipse([x0 * S, y0 * S, x1 * S, y1 * S], fill=col, outline=col[:3] + (230,), width=2)

# ---------------- solid things ----------------
R(0, 745, W, H, HARD)                                              # floor tiles
E(315, 770, 1220, 905, SOFT)                                       # rug (soft pile on the floor)
P([(337, 620), (1092, 620), (1092, 665), (337, 665)], HARD)        # dais top tier surface (marble)
P([(337, 665), (1092, 665), (1092, 697), (337, 697)], HARD)        # dais top tier riser
P([(283, 680), (1090, 680), (1090, 712), (283, 712)], HARD)        # dais lower tier surface
P([(283, 712), (1090, 712), (1090, 745), (283, 745)], HARD)        # dais lower tier riser
P([(560, 535), (970, 535), (952, 612), (905, 636), (650, 636), (592, 612)], HARD)   # tub body
E(553, 506, 980, 546, HARD)                                        # tub rim
R(677, 438, 745, 512, HARD)                                        # brass taps (tub)
R(882, 438, 935, 510, SOFT)                                        # vase on the tub rim (Codi: diggable)
R(467, 551, 539, 627, MAYBE)                                       # plant pot (ceramic - needs a call)
E(410, 275, 600, 552, SOFT)                                        # big floor plant leaves
R(38, 497, 155, 615, HARD); R(65, 598, 232, 748, HARD)             # toilet tank + bowl
R(252, 488, 435, 516, HARD)                                        # brass grab bar
R(1082, 545, 1540, 575, HARD)                                      # vanity marble counter
R(1090, 575, 1535, 725, SOFT)                                      # vanity cabinet (timber)
R(1485, 725, 1502, 752, HARD); R(1515, 725, 1530, 748, HARD)       # brass vanity legs
R(1193, 497, 1385, 553, HARD)                                      # sink bowl
R(1262, 437, 1335, 497, HARD)                                      # sink faucet (brass)
R(1445, 475, 1500, 555, SOFT)                                      # vase on the vanity
R(1080, 680, 1232, 735, SOFT)                                      # stool cushion
for lx0, lx1 in ((1085, 1105), (1130, 1142), (1160, 1182), (1208, 1226)): R(lx0, 737, lx1, 782, HARD)   # brass stool legs
for (a, b, c, e) in ((18, 165, 215, 198), (8, 328, 215, 352)): R(a, b, c, e, SOFT)          # left wall shelves (timber)
R(955, 262, 1140, 277, MAYBE); R(955, 350, 1140, 365, MAYBE)       # right gold wall shelves (metal or timber?)
for y in (437, 524, 609, 694): R(1553, y, 1652, y + 10, SOFT)      # niche shelves (timber)
R(280, 257, 410, 268, SOFT); R(1525, 58, 1648, 70, SOFT)           # picture frame tops (wooden)
P([(1190, 290), (1250, 175), (1330, 157), (1445, 250), (1440, 340), (1330, 425), (1230, 400)], HARD)  # mirror (glass + brass frame)
R(1148, 225, 1182, 345, HARD); R(1457, 210, 1495, 343, HARD)       # wall sconces (brass)
E(540, 0, 710, 220, HARD); E(1355, 70, 1460, 175, HARD)            # pendant lamp globes
R(255, 100, 395, 245, HARD)                                        # sunburst clock (brass)
R(485, 105, 955, 492, MAYBE)                                       # window (glass + black frame)
R(18, 75, 190, 175, DECOR); R(150, 195, 200, 415, DECOR)           # hanging plant + vine (left)
R(1580, 45, 1672, 320, DECOR); R(975, 205, 1060, 275, DECOR); R(990, 285, 1050, 460, DECOR)   # hanging plants (right)
R(45, 235, 100, 330, MAYBE)                                        # black ceramic cat statue
R(850, 330, 955, 440, DECOR); R(1430, 380, 1530, 475, DECOR)       # lilies in the vases
R(1525, 62, 1648, 318, DECOR); R(280, 257, 410, 462, DECOR)        # picture art itself

# ---------------- walkable surfaces ----------------
walk = [  # (number, x0, x1, y, label colour)
    (1, 0, W, 785), (2, 283, 1090, 706), (3, 337, 1092, 650), (4, 553, 980, 526), (5, 40, 155, 497), (6, 68, 230, 598),
    (7, 252, 435, 490), (8, 18, 215, 168), (9, 8, 215, 329), (10, 280, 410, 257), (11, 485, 955, 488),
    (12, 955, 1140, 266), (13, 955, 1140, 356), (14, 1525, 1648, 58), (15, 1555, 1650, 437), (16, 1555, 1650, 524),
    (17, 1555, 1650, 609), (18, 1555, 1650, 694), (19, 1082, 1540, 556), (20, 1193, 1385, 497), (21, 1080, 1232, 680),
    (22, 468, 538, 551),
]
for n, x0, x1, y, in [(w[0], w[1], w[2], w[3]) for w in walk]:
    col = (0, 255, 255, 255) if n == 1 else (255, 240, 0, 255)
    d.line([(x0 * S, y * S), (x1 * S, y * S)], fill=col, width=6)
out = Image.alpha_composite(base, ov)
d2 = ImageDraw.Draw(out)
for n, x0, x1, y in [(w[0], w[1], w[2], w[3]) for w in walk]:
    cx = x0 + 24 if n != 1 else 90
    d2.ellipse([(cx - 13) * S, (y - 24) * S, (cx + 13) * S, (y + 2) * S], fill=(0, 0, 0, 230), outline=(255, 240, 0, 255), width=2)
    d2.text((cx * S, (y - 11) * S), str(n), fill=(255, 240, 0, 255), font=font, anchor="mm")
# legend
d2.rectangle([20, 20, 760, 250], fill=(0, 0, 0, 215))
for i, (col, txt) in enumerate([(SOFT, "SOFT - can be dug (timber, cushions, rug, plants, vases)"), (HARD, "HARD - can't be dug (marble, porcelain, brass, tile, glass)"),
                                (MAYBE, "UNSURE - needs your call (ceramic pots, gold shelves, window, statue)"),
                                (DECOR, "DECORATION - not solid (vines, flowers, pictures; shown pale)")]):
    d2.rectangle([36, 40 + i * 44, 76, 72 + i * 44], fill=col[:3] + (255,)); d2.text((92, 56 + i * 44), txt, fill=(255, 255, 255, 255), font=font, anchor="lm")
d2.text((36, 222), "Yellow lines = walkable surfaces (numbered).  Cyan = floor lane.", fill=(255, 240, 0, 255), font=font, anchor="lm")
out.convert("RGB").save(os.path.join(HERE, "level2_map_review.png"))
print("saved", out.size)
