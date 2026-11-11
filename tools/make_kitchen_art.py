"""Draws placeholder art for the Kitchen demo level and the kitten sprite sheet.
Outputs PNG data saved as .bytes files (Unity loads .bytes as raw data, so big images aren't resized on import).
Game coordinates: 640x360, y measured UP from the bottom. Art is drawn at 4x (2560x1440), supersampled for smooth edges."""
import os, random, math
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Art")
GW, GH, K = 640, 360, 4          # game size and art scale
SS = 2                           # supersampling
S = K * SS
IW, IH = GW * S, GH * S

def box(x0, y0, x1, y1):
    """game rect (y up) -> PIL box at supersampled resolution"""
    return [x0 * S, IH - y1 * S, x1 * S, IH - y0 * S]
def pt(x, y): return (x * S, IH - y * S)

def save(img, name):
    img = img.resize((img.width // SS, img.height // SS), Image.LANCZOS)
    path = os.path.join(OUT, name + ".bytes")
    img.save(path, format="PNG", optimize=True)
    img.save(os.path.join(os.path.dirname(__file__), name + "_preview.png"))
    print(name, img.size, os.path.getsize(path) // 1024, "KB")

def rrect(d, b, r, fill, outline=None, width=0):
    d.rounded_rectangle(box(*b), radius=r * S, fill=fill, outline=outline, width=width * S if width else 0)

def ellipse(d, cx, cy, rx, ry, fill):
    d.ellipse(box(cx - rx, cy - ry, cx + rx, cy + ry), fill=fill)

INK = (92, 64, 58, 255)          # soft dark outline colour
MINT = (164, 214, 196, 255); MINT_D = (128, 182, 164, 255); MINT_L = (196, 232, 219, 255)
WOOD = (204, 156, 110, 255); WOOD_L = (226, 186, 142, 255); WOOD_D = (170, 122, 84, 255)
STEEL = (205, 212, 218, 255); STEEL_D = (160, 168, 176, 255)
random.seed(7)

# ---------------------------------------------------------------- background
bg = Image.new("RGBA", (IW, IH), (250, 240, 226, 255))
d = ImageDraw.Draw(bg)
# wallpaper: tiny roses and leaves
for row, gy in enumerate(range(160, 360, 22)):
    for gx in range(-10, 660, 26):
        x = gx + (13 if row % 2 else 0) + random.uniform(-3, 3); y = gy + random.uniform(-3, 3)
        ellipse(d, x - 3.2, y - 1.5, 2.6, 1.2, (176, 206, 166, 255)); ellipse(d, x + 3.2, y - 1.8, 2.6, 1.2, (176, 206, 166, 255))
        for a in range(5):
            ang = a * 2 * math.pi / 5 + 0.3
            ellipse(d, x + math.cos(ang) * 1.9, y + math.sin(ang) * 1.9, 1.7, 1.7, (240, 180, 190, 255))
        ellipse(d, x, y, 1.0, 1.0, (250, 222, 150, 255))
# cornice
d.rectangle(box(0, 346, 640, 360), fill=(238, 222, 202, 255)); d.rectangle(box(0, 345, 640, 347), fill=(218, 198, 176, 255))
# tiled backsplash behind the worktops
d.rectangle(box(0, 150, 640, 214), fill=(232, 244, 239, 255))
for x in range(0, 641, 10): d.rectangle(box(x - 0.3, 150, x + 0.3, 214), fill=(206, 222, 215, 255))
for y in range(150, 215, 10): d.rectangle(box(0, y - 0.3, 640, y + 0.3), fill=(206, 222, 215, 255))
d.rectangle(box(0, 213, 640, 216), fill=(214, 230, 223, 255))
# soft shadows for depth (wall just above worktops, and beside the fridge)
for i in range(8):
    a = int(60 * (1 - i / 8))
    d.rectangle(box(0, 150 + i, 640, 151 + i), fill=(90, 70, 60, 0))
sh = Image.new("RGBA", (IW, IH), (0, 0, 0, 0)); sd = ImageDraw.Draw(sh)
sd.rectangle(box(0, 150, 640, 158), fill=(80, 60, 50, 70)); sd.rectangle(box(350, 150, 362, 334), fill=(80, 60, 50, 60))
sd.rectangle(box(15, 196, 152, 200), fill=(80, 60, 50, 60))
sh = sh.filter(ImageFilter.GaussianBlur(6 * S)); bg = Image.alpha_composite(bg, sh); d = ImageDraw.Draw(bg)
# window
rrect(d, (22, 208, 148, 338), 3, (252, 250, 244, 255), INK, 1)
sky = Image.new("RGBA", ((140 - 30) * S, (330 - 216) * S))
for yy in range(sky.height):
    t = yy / sky.height
    c = (int(150 + 70 * t), int(205 + 35 * t), int(240 + 10 * t), 255)
    ImageDraw.Draw(sky).line([(0, yy), (sky.width, yy)], fill=c)
bg.paste(sky, (30 * S, IH - 330 * S)); d = ImageDraw.Draw(bg)
for cx, cy, r in [(55, 305, 9), (66, 309, 11), (78, 304, 8), (112, 290, 7), (121, 293, 9)]:
    ellipse(d, cx, cy, r, r * 0.7, (255, 255, 255, 235))
for i, (cx, r, col) in enumerate([(50, 30, (150, 200, 140, 255)), (100, 38, (130, 188, 128, 255)), (140, 25, (150, 200, 140, 255))]):
    d.pieslice(box(cx - r, 216 - r * 0.8, cx + r, 216 + r * 0.8), 180, 360, fill=col)
d.rectangle(box(30, 210, 140, 216), fill=(252, 250, 244, 255))
d.rectangle(box(83, 216, 87, 330), fill=(252, 250, 244, 255)); d.rectangle(box(30, 268, 140, 272), fill=(252, 250, 244, 255))
# curtains + rod
for x0, x1, flip in [(10, 40, False), (130, 160, True)]:
    poly = [pt(x0, 340), pt(x1, 340), pt(x1 - (0 if flip else 14), 200), pt(x0 + (14 if flip else 0), 200)]
    d.polygon(poly, fill=(246, 206, 206, 255))
    for k in range(4):
        fx = x0 + 4 + k * 7
        d.line([pt(fx, 338), pt(fx + (5 if flip else -3), 202)], fill=(226, 176, 180, 255), width=2 * S)
    for k in range(18):
        ellipse(d, random.uniform(x0 + 3, x1 - 6), random.uniform(205, 335), 1.2, 1.2, (238, 150, 165, 255))
d.rectangle(box(6, 339, 164, 342), fill=(150, 104, 80, 255)); ellipse(d, 6, 340.5, 3, 3, (150, 104, 80, 255)); ellipse(d, 164, 340.5, 3, 3, (150, 104, 80, 255))
# little plant on the sill
d.polygon([pt(132, 210), pt(146, 210), pt(144, 221), pt(134, 221)], fill=(214, 128, 96, 255))
for ang in (-50, -20, 10, 40, 70):
    a = math.radians(ang + 90)
    ellipse(d, 139 + math.cos(a) * 7, 226 + math.sin(a) * 6, 3.5, 2, (110, 170, 100, 255))
# sill brackets
for bx in (30, 135): d.polygon([pt(bx, 200), pt(bx + 8, 200), pt(bx + 8, 192)], fill=(236, 230, 218, 255))
# tap over the sink
d.rectangle(box(226, 150, 230, 178), fill=STEEL); d.arc(box(214, 168, 238, 188), 0, 180, fill=STEEL, width=4 * S)
d.rectangle(box(213, 168, 217, 176), fill=STEEL); ellipse(d, 228, 152, 6, 2, STEEL_D)
# pan rail and pans above the stove
d.rectangle(box(356, 300, 444, 302), fill=(150, 104, 80, 255))
for hx, kind in [(372, "pan"), (400, "ladle"), (428, "copper")]:
    d.line([pt(hx, 300), pt(hx, 292)], fill=(120, 110, 100, 255), width=S)
    if kind == "pan":
        d.rectangle(box(hx - 1.5, 270, hx + 1.5, 292), fill=(70, 70, 76, 255)); ellipse(d, hx, 260, 12, 12, (70, 70, 76, 255)); ellipse(d, hx, 260, 9, 9, (95, 95, 102, 255))
    elif kind == "ladle":
        d.rectangle(box(hx - 1, 262, hx + 1, 292), fill=STEEL_D); d.pieslice(box(hx - 7, 252, hx + 7, 266), 0, 180, fill=STEEL_D)
    else:
        d.rectangle(box(hx - 1.5, 272, hx + 1.5, 292), fill=(170, 100, 60, 255)); ellipse(d, hx, 262, 11, 11, (200, 120, 80, 255)); ellipse(d, hx, 262, 8, 8, (225, 150, 105, 255))
# stove back panel
rrect(d, (352, 150, 448, 178), 3, MINT, INK, 1)
for kx in (368, 384, 416, 432): ellipse(d, kx, 164, 4, 4, (250, 248, 240, 255))
ellipse(d, 400, 164, 7, 7, (250, 248, 240, 255)); d.line([pt(400, 164), pt(400, 169)], fill=INK, width=S); d.line([pt(400, 164), pt(403, 164)], fill=INK, width=S)
# shelf with jars
d.rectangle(box(462, 226, 632, 230), fill=WOOD); d.rectangle(box(462, 225, 632, 226.5), fill=WOOD_D)
for jx, w, h, lid in [(475, 14, 22, (240, 180, 190, 255)), (495, 12, 17, MINT), (513, 14, 26, (250, 222, 150, 255))]:
    rrect(d, (jx - w / 2, 230, jx + w / 2, 230 + h), 3, (228, 240, 244, 230), (180, 200, 206, 255), 1)
    rrect(d, (jx - w / 2 - 1, 230 + h - 1, jx + w / 2 + 1, 230 + h + 3), 1, lid)
rrect(d, (545, 230, 575, 246), 6, (250, 248, 240, 255), INK, 1)          # teapot
d.polygon([pt(575, 240), pt(586, 247), pt(584, 249), pt(575, 244)], fill=(250, 248, 240, 255)); ellipse(d, 560, 248, 4, 2, MINT)
d.polygon([pt(600, 230), pt(618, 230), pt(615, 243), pt(603, 243)], fill=(214, 128, 96, 255))
for ang in range(-80, 81, 32):
    a = math.radians(ang + 90); ellipse(d, 609 + math.cos(a) * 9, 252 + math.sin(a) * 8, 4, 2.2, (110, 170, 100, 255))
# wall clock
ellipse(d, 530, 300, 16, 16, MINT_D); ellipse(d, 530, 300, 13, 13, (252, 250, 244, 255))
d.line([pt(530, 300), pt(530, 310)], fill=INK, width=2 * S); d.line([pt(530, 300), pt(537, 300)], fill=INK, width=2 * S)
# cat basket (the exit) on the right worktop
d.pieslice(box(558, 136, 602, 176), 180, 360, fill=(206, 156, 100, 255))
for k in range(5): d.arc(box(560 + k, 138 + k, 600 - k, 174 - k), 180, 360, fill=(176, 126, 76, 255), width=S)
ellipse(d, 580, 156, 20, 4, (246, 190, 200, 255)); ellipse(d, 580, 157, 15, 2.5, (236, 170, 184, 255))
ellipse(d, 580, 182, 4, 3.5, (238, 120, 140, 255)); ellipse(d, 576, 184, 3, 3, (238, 120, 140, 255)); ellipse(d, 584, 184, 3, 3, (238, 120, 140, 255))
d.polygon([pt(573, 183), pt(587, 183), pt(580, 175)], fill=(238, 120, 140, 255))   # little heart
# food bowl
d.pieslice(box(610, 144, 630, 158), 180, 360, fill=(240, 150, 160, 255)); ellipse(d, 620, 151, 10, 2, (200, 140, 90, 255))
save(bg, "kitchen_bg")

# ---------------------------------------------------------------- foreground (solid stuff)
fg = Image.new("RGBA", (IW, IH), (0, 0, 0, 0))
d = ImageDraw.Draw(fg)
# floor tiles
for i, x in enumerate(range(0, 640, 20)):
    for j, y in enumerate(range(0, 40, 20)):
        col = (246, 232, 214, 255) if (i + j) % 2 == 0 else (226, 170, 150, 255)
        d.rectangle(box(x, y, x + 20, y + 20), fill=col)
d.rectangle(box(0, 37, 640, 40), fill=(214, 196, 178, 255))
def cabinets(x0, x1, drawers=False):
    d.rectangle(box(x0, 40, x1, 140), fill=MINT)
    d.rectangle(box(x0, 40, x1, 47), fill=MINT_D)
    n = max(1, round((x1 - x0) / 52)); w = (x1 - x0) / n
    for k in range(n):
        a, b = x0 + k * w + 3, x0 + (k + 1) * w - 3
        if drawers:
            rrect(d, (a, 118, b, 136), 2, MINT_L, MINT_D, 1); ellipse(d, (a + b) / 2, 127, 5, 1.5, (210, 180, 110, 255))
            top = 114
        else:
            top = 136
        rrect(d, (a, 51, b, top), 3, MINT_L, MINT_D, 1)
        rrect(d, (a + 5, 57, b - 5, top - 6), 2, MINT, None)
        ellipse(d, b - 7 if k % 2 == 0 else a + 7, (51 + top) / 2 + 8, 2.2, 2.2, (210, 180, 110, 255))
def worktop(x0, x1):
    d.rectangle(box(x0, 140, x1, 150), fill=WOOD)
    d.rectangle(box(x0, 148, x1, 150), fill=WOOD_L); d.rectangle(box(x0, 140, x1, 141.5), fill=WOOD_D)
    for x in range(int(x0) + 23, int(x1), 31): d.rectangle(box(x, 141.5, x + 0.6, 148), fill=WOOD_D)
cabinets(0, 270); worktop(0, 272)
cabinets(450, 640, drawers=True); worktop(448, 640)
# sink: steel basin cut into the left worktop (walls 200-208 and 248-256, bottom 104-112, open inside)
d.rectangle(box(200, 104, 256, 150), fill=STEEL)
d.rectangle(box(208, 112, 248, 150), fill=(0, 0, 0, 0))
d.rectangle(box(200, 148, 256, 150), fill=(232, 236, 240, 255))
d.rectangle(box(208, 112, 248, 118), fill=(140, 190, 230, 110))     # water - see-through, not solid
d.rectangle(box(201, 105, 203, 147), fill=(232, 236, 240, 255)); d.rectangle(box(253, 105, 255, 147), fill=STEEL_D)
# window sill
rrect(d, (14, 200, 152, 210), 2, (240, 234, 222, 255), INK, 1)
d.rectangle(box(15, 208, 151, 210), fill=(252, 250, 244, 255))
# fridge
rrect(d, (270, 44, 350, 330), 14, (170, 220, 205, 255), INK, 1)
d.rectangle(box(272, 255, 348, 258), fill=MINT_D)
d.rectangle(box(276, 60, 282, 250), fill=(200, 236, 224, 255)); d.rectangle(box(276, 265, 282, 320), fill=(200, 236, 224, 255))
rrect(d, (336, 190, 341, 240), 2, (232, 236, 240, 255), STEEL_D, 1); rrect(d, (336, 268, 341, 300), 2, (232, 236, 240, 255), STEEL_D, 1)
d.rectangle(box(276, 40, 286, 46), fill=(90, 90, 96, 255)); d.rectangle(box(334, 40, 344, 46), fill=(90, 90, 96, 255))
for mx, my, col in [(300, 222, (240, 120, 140, 255)), (318, 230, (250, 210, 100, 255)), (306, 200, (120, 170, 230, 255))]:
    ellipse(d, mx, my, 3.5, 3.5, col)
rrect(d, (296, 150, 326, 182), 1, (255, 255, 255, 255), (200, 200, 200, 255), 1)          # kid's drawing of a cat
d.polygon([pt(304, 160), pt(318, 160), pt(318, 170), pt(304, 170)], fill=(250, 222, 190, 255))
d.polygon([pt(304, 170), pt(307, 176), pt(309, 170)], fill=(120, 90, 80, 255)); d.polygon([pt(313, 170), pt(315, 176), pt(318, 170)], fill=(120, 90, 80, 255))
# stove
d.rectangle(box(350, 40, 450, 150), fill=MINT)
d.rectangle(box(350, 40, 450, 47), fill=MINT_D)
rrect(d, (360, 62, 440, 120), 4, (250, 248, 240, 255), INK, 1)
rrect(d, (368, 70, 432, 112), 3, (70, 64, 70, 255)); d.polygon([pt(372, 108), pt(386, 108), pt(400, 74), pt(392, 74)], fill=(110, 104, 112, 255))
rrect(d, (366, 124, 434, 128), 2, (232, 236, 240, 255), STEEL_D, 1)
for kx in range(366, 440, 17): ellipse(d, kx, 138, 4, 4, (250, 248, 240, 255)); d.line([pt(kx, 138), pt(kx, 141)], fill=INK, width=S)
d.rectangle(box(350, 145, 450, 150), fill=(80, 80, 86, 255))
for bx in (372, 428): d.rectangle(box(bx - 12, 150, bx + 12, 152), fill=(60, 60, 64, 255))
fg = fg.filter(ImageFilter.SMOOTH) if False else fg
save(fg, "kitchen_fg")

# ---------------------------------------------------------------- kitten sprite sheet (your cat!)
FW, FH, CS = 48, 40, 8           # frame size at art scale, supersample
CREAM = (246, 234, 214, 255); SHADE = (226, 206, 182, 255); POINT = (96, 66, 54, 255); POINT_L = (138, 100, 80, 255)
WHITE = (255, 252, 246, 255); EYE = (110, 182, 236, 255)

def kitten(legs, tail_up=0.0, ears_up=0.0, mouth_open=False):
    im = Image.new("RGBA", (FW * CS, FH * CS), (0, 0, 0, 0)); dd = ImageDraw.Draw(im)
    def e(cx, cy, rx, ry, col): dd.ellipse([(cx - rx) * CS, (cy - ry) * CS, (cx + rx) * CS, (cy + ry) * CS], fill=col)
    def poly(pts, col): dd.polygon([(x * CS, y * CS) for x, y in pts], fill=col)
    # tail (fluffy, dark)
    for i in range(7):
        t = i / 6
        tx = 12 - 7 * t; ty = 24 - (10 + 6 * tail_up) * t * t - 2 * t
        e(tx, ty, 3.4 - t * 0.6, 3.4 - t * 0.6, POINT if t > 0.3 else POINT_L)
    # back legs, body, front legs
    for lx, ly in legs[:2]: e(lx, 33 + ly * 0.3, 3, 5, SHADE); e(lx + 0.5, 37.2 + ly * 0.2, 3.1, 2.2, POINT)
    e(22, 26, 13, 9, CREAM); e(22, 29, 11, 5, SHADE); e(20, 22, 10, 5, CREAM)
    for lx, ly in legs[2:]: e(lx, 33 + ly * 0.3, 3, 5, CREAM); e(lx + 0.5, 37.2 + ly * 0.2, 3.1, 2.2, POINT)
    e(31, 26, 6, 6, WHITE)                                     # fluffy chest
    # head
    hx, hy = 34, 15
    poly([(hx - 9, hy - 3), (hx - 7, hy - 14 - ears_up), (hx - 1, hy - 7)], POINT)
    poly([(hx + 2, hy - 7), (hx + 8, hy - 14 - ears_up), (hx + 10, hy - 2)], POINT)
    poly([(hx - 7, hy - 5), (hx - 6.3, hy - 11 - ears_up), (hx - 3, hy - 7)], (236, 160, 170, 255))
    e(hx, hy, 11, 10, CREAM)
    e(hx + 2, hy + 3.5, 7.5, 5.5, POINT_L); e(hx + 2.5, hy + 4.5, 5.5, 4, POINT)   # seal-point mask
    for ex in (hx - 3.2, hx + 5.2):
        e(ex, hy - 0.5, 2.9, 3.2, EYE); e(ex + 0.4, hy - 0.2, 1.1, 2.2, (30, 30, 40, 255)); e(ex - 0.8, hy - 1.6, 0.9, 0.9, WHITE)
    e(hx + 2.5, hy + 4.2, 1.4, 1.0, (240, 150, 165, 255))
    if mouth_open: e(hx + 2.5, hy + 7, 1.6, 1.2, (200, 90, 110, 255))
    # outline: dilate the silhouette and put it behind
    alpha = im.split()[3].filter(ImageFilter.MaxFilter(2 * CS + 1))
    outline = Image.new("RGBA", im.size, (80, 56, 48, 255)); outline.putalpha(alpha)
    out = Image.alpha_composite(outline, im)
    return out.resize((FW, FH), Image.LANCZOS)

frames = [
    kitten([(13, 0), (18, -2), (27, -2), (32, 0)]),
    kitten([(14, -2), (17, 0), (28, 0), (31, -2)], tail_up=0.3),
    kitten([(13, 0), (18, -2), (27, -2), (32, 0)], tail_up=0.6),
    kitten([(12, -2), (19, 0), (26, 0), (33, -2)], tail_up=0.3),
    kitten([(10, -6), (20, -6), (26, -6), (36, -6)], tail_up=1.0, ears_up=2, mouth_open=True),   # falling
    kitten([(15, 0), (17, 0), (29, 0), (31, 0)], tail_up=-0.2),                                   # sitting / kneading
]
sheet = Image.new("RGBA", (FW * len(frames), FH), (0, 0, 0, 0))
for i, f in enumerate(frames): sheet.paste(f, (i * FW, 0))
p = os.path.join(OUT, "cat_sheet.bytes"); sheet.save(p, format="PNG")
sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save(os.path.join(os.path.dirname(__file__), "cat_sheet_preview.png"))
print("cat_sheet", sheet.size)
