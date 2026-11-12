"""Draws placeholder art for the big Kitchen demo level (960x540 game pixels, art at 3x = 2880x1620),
plus the props and secret items. Saved as PNG data in .bytes files (Unity loads .bytes as raw data).
Game coordinates: y measured UP from the bottom. Everything is supersampled for smooth edges.

Layout (game pixels):
  floor tiles 0-40 (stone) | window sill top 316 (x30-215) | shelf top 258 (x205-300)
  cat tree in the corner (platforms 258/200/145/90), pantry 112-136, left worktop top 200 (x136-560, cabinets on legs) with sink (outer 336-392, water inside) and toaster (466-506)
  fridge 560-664 from 64 up to 480 (metal), on legs with a gap underneath 40-64 | stove 664-784, top 200, gas burners
  right worktop 784-880 top 200 on legs (open underneath) | cat bed (exit) on the floor ~924
"""
import os, random, math
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(__file__)
OUT = os.path.join(HERE, "..", "Assets", "Resources", "Art")
GW, GH, K = 960, 540, 3
SS = 2
S = K * SS
IW, IH = GW * S, GH * S

def box(x0, y0, x1, y1, h=IH):
    return [x0 * S, h - y1 * S, x1 * S, h - y0 * S]
def pt(x, y, h=IH): return (x * S, h - y * S)

def save(img, name, preview=True):
    img = img.resize((img.width // SS, img.height // SS), Image.LANCZOS)
    path = os.path.join(OUT, name + ".bytes")
    img.save(path, format="PNG", optimize=True)
    if preview: img.save(os.path.join(HERE, name + "_preview.png"))
    print(name, img.size, os.path.getsize(path) // 1024, "KB")
    return img

class Pen:
    """Drawing helpers in game coordinates for one image of game-size (w, h)."""
    def __init__(self, img, h):
        self.d = ImageDraw.Draw(img); self.h = h * S
    def b(self, x0, y0, x1, y1): return box(x0, y0, x1, y1, self.h)
    def p(self, x, y): return pt(x, y, self.h)
    def rect(self, x0, y0, x1, y1, fill): self.d.rectangle(self.b(x0, y0, x1, y1), fill=fill)
    def rrect(self, x0, y0, x1, y1, r, fill, outline=None, width=0, top_only=False):
        corners = (True, True, False, False) if top_only else None
        self.d.rounded_rectangle(self.b(x0, y0, x1, y1), radius=r * S, fill=fill, outline=outline, width=int(width * S), corners=corners)
    def ell(self, cx, cy, rx, ry, fill, outline=None, width=0):
        self.d.ellipse(self.b(cx - rx, cy - ry, cx + rx, cy + ry), fill=fill, outline=outline, width=int(width * S))
    def poly(self, pts, fill): self.d.polygon([self.p(x, y) for x, y in pts], fill=fill)
    def line(self, pts, fill, width): self.d.line([self.p(x, y) for x, y in pts], fill=fill, width=int(width * S))
    def arc(self, x0, y0, x1, y1, a0, a1, fill, width): self.d.arc(self.b(x0, y0, x1, y1), a0, a1, fill=fill, width=int(width * S))
    def pie(self, x0, y0, x1, y1, a0, a1, fill): self.d.pieslice(self.b(x0, y0, x1, y1), a0, a1, fill=fill)

INK = (92, 64, 58, 255)
MINT = (164, 214, 196, 255); MINT_D = (128, 182, 164, 255); MINT_L = (196, 232, 219, 255)
WOOD = (204, 156, 110, 255); WOOD_L = (226, 186, 142, 255); WOOD_D = (170, 122, 84, 255)
STEEL = (205, 212, 218, 255); STEEL_D = (150, 158, 168, 255); STEEL_L = (236, 240, 244, 255)
CREAM = (252, 250, 244, 255); BRASS = (210, 180, 110, 255)
GOLD = (246, 200, 70, 255); GOLD_D = (200, 150, 40, 255); GOLD_L = (255, 236, 150, 255)
random.seed(11)

def soft_shadow(img, rects, blur=6, alpha=70):
    sh = Image.new("RGBA", img.size, (0, 0, 0, 0)); sd = Pen(sh, GH)
    for r in rects: sd.rect(*r, (70, 50, 42, alpha))
    sh = sh.filter(ImageFilter.GaussianBlur(blur * S))
    return Image.alpha_composite(img, sh)

# =============================================================== BACKGROUND
bg = Image.new("RGBA", (IW, IH), (250, 240, 226, 255)); P = Pen(bg, GH)
for row, gy in enumerate(range(212, 530, 22)):                    # rose wallpaper
    for gx in range(-10, 980, 26):
        x = gx + (13 if row % 2 else 0) + random.uniform(-3, 3); y = gy + random.uniform(-3, 3)
        P.ell(x - 3.2, y - 1.5, 2.6, 1.2, (176, 206, 166, 255)); P.ell(x + 3.2, y - 1.8, 2.6, 1.2, (176, 206, 166, 255))
        for a in range(5):
            ang = a * 2 * math.pi / 5 + 0.3
            P.ell(x + math.cos(ang) * 1.9, y + math.sin(ang) * 1.9, 1.7, 1.7, (240, 180, 190, 255))
        P.ell(x, y, 1.0, 1.0, (250, 222, 150, 255))
P.rect(0, 40, 960, 212, (238, 232, 220, 255))                     # lower wall (painted)
P.rect(0, 524, 960, 540, (238, 222, 202, 255)); P.rect(0, 522, 960, 524, (218, 198, 176, 255))   # cornice
P.rect(0, 200, 560, 262, (232, 244, 239, 255)); P.rect(664, 200, 960, 262, (232, 244, 239, 255))  # tiled backsplash
for x in range(0, 961, 10):
    if 560 < x < 664: continue
    P.rect(x - 0.3, 200, x + 0.3, 262, (206, 222, 215, 255))
for y in range(200, 263, 10):
    P.rect(0, y - 0.3, 560, y + 0.3, (206, 222, 215, 255)); P.rect(664, y - 0.3, 960, y + 0.3, (206, 222, 215, 255))
P.rect(0, 40, 960, 46, (214, 200, 184, 255))                      # skirting
# wall cabinets (decoration only)
for x0 in (300, 400):
    P.rrect(x0 + 3, 392, x0 + 97, 486, 3, MINT, INK, 0.7)
    for k in range(2):
        a = x0 + 6 + k * 46
        P.rrect(a, 396, a + 42, 482, 3, MINT_L, MINT_D, 0.7); P.rrect(a + 5, 402, a + 37, 476, 2, MINT)
        P.ell(a + (36 if k == 0 else 6), 410, 2, 2, BRASS)
# window + curtains
P.rrect(36, 318, 206, 492, 3, CREAM, INK, 1)
sky = Image.new("RGBA", ((198 - 44) * S, (484 - 326) * S)); sdraw = ImageDraw.Draw(sky)
for yy in range(sky.height):
    t = yy / sky.height
    sdraw.line([(0, yy), (sky.width, yy)], fill=(int(150 + 70 * t), int(205 + 35 * t), int(240 + 10 * t), 255))
bg.paste(sky, (44 * S, IH - 484 * S)); P = Pen(bg, GH)
for cx, cy, r in [(70, 455, 10), (84, 460, 12), (98, 454, 9), (150, 438, 8), (162, 442, 10)]:
    P.ell(cx, cy, r, r * 0.7, (255, 255, 255, 235))
for cx, r, col in [(70, 40, (150, 200, 140, 255)), (135, 50, (130, 188, 128, 255)), (196, 32, (150, 200, 140, 255))]:
    P.pie(cx - r, 326 - r * 0.8, cx + r, 326 + r * 0.8, 180, 360, col)
P.rect(44, 318, 198, 326, CREAM)
P.rect(119, 326, 123, 484, CREAM); P.rect(44, 398, 198, 402, CREAM)
for x0, x1, flip in [(22, 58, False), (184, 220, True)]:
    P.poly([(x0, 496), (x1, 496), (x1 - (0 if flip else 16), 300), (x0 + (16 if flip else 0), 300)], (246, 206, 206, 255))
    for k in range(5):
        fx = x0 + 4 + k * 7
        P.line([(fx, 494), (fx + (6 if flip else -4), 302)], (226, 176, 180, 255), 2)
    for k in range(24):
        P.ell(random.uniform(x0 + 3, x1 - 6), random.uniform(305, 490), 1.2, 1.2, (238, 150, 165, 255))
P.rect(16, 495, 226, 498, (150, 104, 80, 255)); P.ell(16, 496.5, 3, 3, (150, 104, 80, 255)); P.ell(226, 496.5, 3, 3, (150, 104, 80, 255))
for bx in (40, 196): P.poly([(bx, 306), (bx + 8, 306), (bx + 8, 298)], (236, 230, 218, 255))   # sill brackets
for bx in (214, 288): P.poly([(bx, 252), (bx + 6, 252), (bx + 6, 244)], WOOD_D)                 # shelf brackets
# tap over the sink
P.rect(372, 200, 376, 232, STEEL); P.arc(358, 220, 386, 244, 0, 180, STEEL, 4)
P.rect(357, 220, 361, 229, STEEL); P.ell(374, 202, 6, 2, STEEL_D)
# little legs under the cabinets and pantry
for lx in list(range(140, 560, 56)) + [556, 114, 130]:
    P.rect(lx, 40, lx + 3, 54, (120, 100, 90, 255))
# cat tree posts (sisal rope) and a dangling toy
for px, top in [(24, 252), (80, 194)]:
    P.rect(px, 40, px + 8, top, (200, 170, 120, 255))
    for yy in range(42, top, 3): P.rect(px, yy, px + 8, yy + 0.8, (176, 146, 100, 255))
P.line([(60, 252), (60, 228)], (120, 100, 90, 255), 0.6); P.ell(60, 225, 3, 3, (240, 120, 140, 255))
P.rrect(4, 40, 108, 46, 3, (214, 190, 160, 255))
# fridge feet + shadow under fridge and stove
P.poly([(566, 40), (574, 40), (578, 64), (568, 64)], (110, 110, 118, 255)); P.poly([(650, 40), (658, 40), (656, 64), (646, 64)], (110, 110, 118, 255))
P.rect(670, 40, 680, 52, (90, 90, 96, 255)); P.rect(768, 40, 778, 52, (90, 90, 96, 255))
# stove back panel + range hood + hanging rail
P.rrect(666, 200, 782, 236, 3, MINT, INK, 1)
for kx in (684, 704, 744, 764): P.ell(kx, 218, 4.5, 4.5, CREAM)
P.ell(724, 218, 8, 8, CREAM); P.line([(724, 218), (724, 224)], INK, 1); P.line([(724, 218), (728, 218)], INK, 1)
P.poly([(672, 400), (776, 400), (760, 440), (688, 440)], STEEL); P.rect(700, 440, 748, 522, STEEL)
P.rect(672, 398, 776, 402, STEEL_D); P.rect(704, 440, 708, 522, STEEL_L)
for hx, kind in [(694, "ladle"), (724, "whisk"), (754, "copper")]:
    P.line([(hx, 398), (hx, 390)], (120, 110, 100, 255), 1)
    if kind == "ladle":
        P.rect(hx - 1, 360, hx + 1, 390, STEEL_D); P.pie(hx - 7, 350, hx + 7, 364, 0, 180, STEEL_D)
    elif kind == "whisk":
        P.rect(hx - 1, 372, hx + 1, 390, WOOD_D)
        for k in range(-3, 4): P.line([(hx, 372), (hx + k * 1.6, 356), (hx, 352)], STEEL_D, 0.8)
    else:
        P.rect(hx - 1.5, 372, hx + 1.5, 390, (170, 100, 60, 255)); P.ell(hx, 362, 11, 11, (200, 120, 80, 255)); P.ell(hx, 362, 8, 8, (225, 150, 105, 255))
# pan hook over the right worktop (the frying pan itself is a separate prop)
P.rect(808, 262, 890, 264, (150, 104, 80, 255)); P.ell(808, 263, 2, 2, (150, 104, 80, 255)); P.ell(890, 263, 2, 2, (150, 104, 80, 255))
P.ell(872, 238, 8, 8, (200, 120, 80, 255)); P.rect(871, 245, 873, 262, (170, 100, 60, 255)); P.ell(872, 238, 5.5, 5.5, (225, 150, 105, 255))
# right worktop legs + open shelf underneath (decoration, kittens walk through)
for lx in (788, 872): P.rect(lx, 40, lx + 6, 190, WOOD_D)
P.rect(788, 104, 878, 108, WOOD)
for bx, col in [(800, (240, 180, 190, 255)), (812, (180, 210, 230, 255)), (824, (250, 222, 150, 255))]:
    P.rrect(bx, 108, bx + 10, 140, 2, col, INK, 0.5)
P.rrect(842, 108, 866, 126, 6, (250, 248, 240, 255), INK, 0.7)
# calendar, clock, radiator, cat bed (exit), food bowl, rug
P.rrect(812, 400, 860, 460, 2, CREAM, INK, 0.8); P.rect(812, 446, 860, 460, (238, 120, 140, 255))
for gy in range(406, 444, 8):
    for gx in range(816, 858, 8): P.rect(gx, gy, gx + 5, gy + 5, (230, 220, 210, 255))
P.ell(900, 440, 17, 17, MINT_D); P.ell(900, 440, 14, 14, CREAM)
P.line([(900, 440), (900, 450)], INK, 2); P.line([(900, 440), (907, 440)], INK, 2)
P.rrect(892, 76, 952, 150, 4, CREAM, INK, 0.8)
for rx in range(898, 948, 7): P.rrect(rx, 80, rx + 4, 146, 2, (240, 236, 228, 255), (200, 196, 188, 255), 0.5)
P.ell(924, 40, 46, 4, (236, 170, 184, 160))                                         # rug
P.pie(898, 26, 950, 76, 180, 360, (206, 156, 100, 255))                              # wicker cat bed
for k in range(5): P.arc(900 + k, 28 + k, 948 - k, 74 - k, 180, 360, (176, 126, 76, 255), 1)
P.ell(924, 50, 22, 4, (246, 190, 200, 255)); P.ell(924, 51, 17, 2.5, (236, 170, 184, 255))
P.ell(924, 84, 5, 4, (238, 120, 140, 255)); P.ell(919, 86, 3.5, 3.5, (238, 120, 140, 255)); P.ell(929, 86, 3.5, 3.5, (238, 120, 140, 255))
P.poly([(916, 85), (932, 85), (924, 75)], (238, 120, 140, 255))
P.pie(866, 34, 888, 50, 180, 360, (240, 150, 160, 255)); P.ell(877, 42, 11, 2, (200, 140, 90, 255))
# soft shadows for depth
bg = soft_shadow(bg, [(0, 200, 560, 208), (664, 200, 960, 208), (664, 236, 676, 480), (30, 296, 215, 306),
                      (205, 248, 300, 252), (560, 40, 664, 62), (664, 40, 784, 50)])
save(bg, "kitchen_bg")

# =============================================================== FOREGROUND (solid)
fg = Image.new("RGBA", (IW, IH), (0, 0, 0, 0)); P = Pen(fg, GH)
for i, x in enumerate(range(0, 960, 20)):                          # floor tiles
    for j, y in enumerate(range(0, 40, 20)):
        P.rect(x, y, x + 20, y + 20, (246, 232, 214, 255) if (i + j) % 2 == 0 else (226, 170, 150, 255))
P.rect(0, 37, 960, 40, (214, 196, 178, 255))
def cabinets(x0, x1, top=190, drawers=False, bottom=54):
    P.rect(x0, bottom, x1, top, MINT); P.rect(x0, bottom, x1, bottom + 5, MINT_D)
    n = max(1, round((x1 - x0) / 56)); w = (x1 - x0) / n
    for k in range(n):
        a, b = x0 + k * w + 3, x0 + (k + 1) * w - 3
        dtop = top - 4
        if drawers:
            P.rrect(a, top - 26, b, top - 4, 2, MINT_L, MINT_D, 0.7); P.ell((a + b) / 2, top - 15, 5, 1.5, BRASS)
            dtop = top - 30
        P.rrect(a, bottom + 8, b, dtop, 3, MINT_L, MINT_D, 0.7); P.rrect(a + 5, bottom + 14, b - 5, dtop - 6, 2, MINT)
        P.ell(b - 7 if k % 2 == 0 else a + 7, (bottom + 8 + dtop) / 2 + 8, 2.2, 2.2, BRASS)
def worktop(x0, x1):
    P.rect(x0, 190, x1, 200, WOOD); P.rect(x0, 198, x1, 200, WOOD_L); P.rect(x0, 190, x1, 191.5, WOOD_D)
    for x in range(int(x0) + 23, int(x1), 31): P.rect(x, 191.5, x + 0.6, 198, WOOD_D)
cabinets(136, 560, drawers=True); worktop(136, 560)
# tall pantry cupboard (on legs) between the cat tree corner and the worktop
P.rrect(112, 54, 136, 300, 2, MINT, INK, 0.8, top_only=True)
P.rrect(115, 62, 133, 292, 2, MINT_L, MINT_D, 0.6); P.ell(130, 180, 1.6, 1.6, BRASS)
# cat tree platforms (carpeted) - kittens hop down these to the floor
for x0, y0, x1 in [(0, 252, 70), (50, 194, 112), (0, 139, 70), (50, 84, 112)]:
    P.rrect(x0, y0, x1, y0 + 6, 2, (214, 190, 160, 255), (150, 120, 96, 255), 0.6, top_only=True)
    P.rect(x0 + 1, y0 + 4.5, x1 - 1, y0 + 6, (232, 212, 184, 255))
# sink: walls 336-344 & 384-392, bottom 152-160, open inside (the water is drawn by the game)
P.rect(336, 152, 392, 200, STEEL); P.rect(344, 160, 384, 200, (0, 0, 0, 0))
P.rect(336, 198, 344, 200, STEEL_L); P.rect(384, 198, 392, 200, STEEL_L); P.rect(337, 153, 339, 197, STEEL_L); P.rect(389, 153, 391, 197, STEEL_D)
# toaster (metal)
P.rrect(466, 200, 506, 228, 6, STEEL, INK, 0.8, top_only=True); P.rect(472, 226, 480, 230, (80, 80, 86, 255)); P.rect(490, 226, 498, 230, (80, 80, 86, 255))
P.rect(500, 206, 508, 210, (80, 80, 86, 255)); P.rect(470, 208, 474, 222, STEEL_L)
# window sill + shelf
P.rrect(30, 306, 215, 316, 2, (240, 234, 222, 255), INK, 1); P.rect(31, 314, 214, 316, CREAM)
P.rrect(205, 252, 300, 258, 1.5, WOOD, WOOD_D, 0.6); P.rect(206, 257, 299, 258, WOOD_L)
# fridge (metal), sits on feet - the gap underneath is open
P.rrect(560, 64, 664, 480, 16, (170, 220, 205, 255), INK, 1, top_only=True)
P.rect(562, 372, 662, 376, MINT_D)
P.rect(568, 80, 576, 362, (200, 236, 224, 255)); P.rect(568, 386, 576, 466, (200, 236, 224, 255))
P.rrect(648, 260, 655, 330, 2, STEEL_L, STEEL_D, 0.7); P.rrect(648, 390, 655, 430, 2, STEEL_L, STEEL_D, 0.7)
for mx, my, col in [(592, 330, (240, 120, 140, 255)), (614, 338, (250, 210, 100, 255)), (600, 306, (120, 170, 230, 255))]:
    P.ell(mx, my, 4, 4, col)
P.rrect(590, 230, 630, 272, 1, (255, 255, 255, 255), (200, 200, 200, 255), 0.7)        # kid's drawing of a cat
P.poly([(600, 240), (620, 240), (620, 254), (600, 254)], (250, 222, 190, 255))
P.poly([(600, 254), (604, 262), (607, 254)], (120, 90, 80, 255)); P.poly([(613, 254), (616, 262), (620, 254)], (120, 90, 80, 255))
# stove (gap underneath for the gas valve)
P.rect(664, 52, 784, 200, MINT); P.rect(664, 52, 784, 58, MINT_D)
P.rrect(676, 76, 772, 146, 4, CREAM, INK, 1)
P.rrect(686, 86, 762, 136, 3, (70, 64, 70, 255)); P.poly([(692, 132), (708, 132), (726, 90), (716, 90)], (110, 104, 112, 255))
P.rrect(682, 152, 766, 157, 2, STEEL_L, STEEL_D, 0.7)
for kx in range(684, 770, 20): P.ell(kx, 172, 4.5, 4.5, CREAM); P.line([(kx, 172), (kx, 176)], INK, 1)
P.rect(664, 186, 784, 200, (70, 70, 76, 255))
for bx in (696, 752): P.rect(bx - 16, 199, bx + 16, 201, (40, 40, 44, 255))
# right worktop (on legs - the legs are background)
worktop(784, 880)
save(fg, "kitchen_fg")

# =============================================================== PROPS + SECRETS (small separate pictures)
def prop(name, w, h, draw):
    img = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0)); pen = Pen(img, h)
    draw(pen, w, h)
    a = img.split()[3].filter(ImageFilter.MaxFilter(2 * SS + 1))                 # soft outline
    ol = Image.new("RGBA", img.size, (80, 56, 48, 255)); ol.putalpha(a)
    out = Image.alpha_composite(ol, img)
    out = out.resize((w * K, h * K), Image.LANCZOS)
    out.save(os.path.join(OUT, name + ".bytes"), format="PNG")
    out.resize((w * K * 2, h * K * 2), Image.LANCZOS).save(os.path.join(HERE, name + "_preview.png"))
    print(name, out.size)

def cookie_jar(P, w, h):
    P.rrect(2, 0, 28, 24, 8, (230, 242, 246, 235), (170, 196, 204, 255), 0.8)
    for cx, cy in [(9, 6), (19, 7), (14, 13), (8, 17), (20, 16)]:
        P.ell(cx, cy, 4.5, 4.5, (206, 156, 100, 255)); P.ell(cx - 1, cy + 1, 0.8, 0.8, (110, 70, 50, 255)); P.ell(cx + 1.5, cy - 1, 0.8, 0.8, (110, 70, 50, 255))
    P.rrect(4, 24, 26, 28, 2, (240, 180, 190, 255)); P.ell(15, 30, 4, 2.5, (240, 180, 190, 255))
def cereal(P, w, h):
    P.rect(1, 0, 24, 36, (240, 120, 100, 255)); P.rect(1, 30, 24, 36, (250, 222, 150, 255))
    P.ell(12.5, 16, 8, 8, (255, 250, 240, 255)); P.ell(12.5, 16, 6, 6, (250, 222, 150, 255))
    for cx, cy in [(10, 17), (14, 14), (13, 19)]: P.ell(cx, cy, 1.6, 1.6, (220, 150, 70, 255))
    P.rect(4, 4, 21, 7, (255, 250, 240, 255))
def pan(P, w, h):
    P.rect(18, 26, 22, 50, (60, 60, 66, 255)); P.ell(20, 47, 2, 2, (200, 200, 200, 255))
    P.ell(20, 14, 18, 14, (60, 60, 66, 255)); P.ell(20, 14, 14.5, 11, (88, 88, 96, 255)); P.ell(15, 18, 4, 2.5, (120, 120, 130, 255))
def sparkle(P, cx, cy, r):
    P.poly([(cx, cy + r), (cx + r * 0.25, cy + r * 0.25), (cx + r, cy), (cx + r * 0.25, cy - r * 0.25), (cx, cy - r),
            (cx - r * 0.25, cy - r * 0.25), (cx - r, cy), (cx - r * 0.25, cy + r * 0.25)], (255, 255, 230, 255))
def yarn(P, w, h):
    P.ell(8, 7, 7, 7, GOLD)
    for k in range(-3, 4): P.arc(1 + k, 0, 15 + k, 14, 200, 340, GOLD_D, 0.7)
    P.line([(13, 3), (16, 1)], GOLD_D, 0.8); sparkle(P, 13, 11, 2.5)
def mouse(P, w, h):
    P.ell(8, 5, 6, 4.5, GOLD); P.ell(12.5, 7.5, 2.3, 2.3, GOLD_L); P.ell(13.5, 4.5, 0.7, 0.7, (80, 56, 48, 255))
    P.ell(15, 4, 0.9, 0.9, (238, 120, 140, 255)); P.line([(2, 4), (0, 6), (1, 9)], GOLD_D, 0.8); sparkle(P, 4, 10, 2.5)
def fishbone(P, w, h):
    P.line([(2, 7), (13, 7)], GOLD, 1.4); P.poly([(13, 7), (16, 10), (16, 4)], GOLD)
    for k in range(4, 12, 2.5 if False else 3): P.line([(k, 4), (k + 1, 7), (k, 10)], GOLD, 1)
    P.ell(2.5, 7, 2.5, 2.2, GOLD); P.ell(2, 7.5, 0.6, 0.6, (80, 56, 48, 255)); sparkle(P, 12, 12, 2.2)
def mousetrap(P, w, h):
    P.rect(0, 0, 24, 3, (200, 156, 100, 255)); P.rect(0, 2.4, 24, 3, (226, 186, 142, 255))
    P.line([(4, 3), (4, 8), (20, 8), (20, 3)], STEEL_D, 1)
    P.rect(11, 3, 13, 5, STEEL_D)
    P.poly([(14, 3), (21, 3), (21, 7)], (250, 210, 90, 255)); P.ell(18, 4.5, 0.8, 0.8, (220, 170, 60, 255))
def valve(P, w, h):
    P.rect(0, 2, 24, 6, STEEL_D); P.rect(10, 4, 14, 8, STEEL_D)
    P.ell(12, 8, 5, 3.5, (220, 70, 70, 255)); P.ell(12, 8, 2, 1.4, (250, 230, 230, 255))
prop("prop_cookiejar", 30, 32, cookie_jar)
prop("prop_cereal", 25, 36, cereal)
prop("prop_pan", 40, 50, pan)
prop("secret_yarn", 16, 14, yarn)
prop("secret_mouse", 16, 12, mouse)
prop("secret_fishbone", 17, 14, fishbone)
prop("valve", 24, 12, valve)
prop("mousetrap", 24, 10, mousetrap)
