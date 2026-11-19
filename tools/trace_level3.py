"""Silk tracing, step 1: finds the walking lines of the Doll House picture to sub-pixel accuracy by snapping to the real marble/face edge.
The picture is only READ. Writes tools/level3_trace.json (walkway ribbons, picture px, y down) and tools/level3_trace_review.png."""
import json, os
import numpy as np
from PIL import Image, ImageDraw
HERE = os.path.dirname(__file__)
img = Image.open(os.path.join(HERE, "art_sources", "bathroom_level3_dollhouse.webp")).convert("RGB")
L = np.asarray(img.convert("L"), np.float32)
H, W = L.shape

def col_edge(x, ys, win=9):
    """sub-pixel y of the strongest bright->dark drop (going down) within +-win of ys, averaged over a 5px-wide strip"""
    y0 = int(ys - win); y1 = int(ys + win) + 1
    prof = np.convolve(L[y0:y1, max(0, int(x) - 2):int(x) + 3].mean(1), np.ones(3) / 3, "same")
    g = np.gradient(prof); k = int(np.argmin(g[2:-2])) + 2
    a, b, c = g[k - 1], g[k], g[k + 1]                       # parabola through the three gradient samples = sub-pixel peak
    den = a - 2 * b + c; off = 0.5 * (a - c) / den if abs(den) > 1e-6 else 0.0
    return y0 + k + max(-1, min(1, off)), -g[k]

def smooth(v, n=5):
    v = np.array(v, float); p = np.pad(v, n, mode="edge"); w = np.hanning(2 * n + 1); w /= w.sum()
    return np.convolve(p, w, "valid")

def trace(seed, step=8, win=5):
    """seed: [(x, approx_y)] -> smooth snapped polyline of the edge (picture px)"""
    sx = np.array([p[0] for p in seed], float); sy = np.array([p[1] for p in seed], float)
    xs = np.arange(sx[0], sx[-1] + 1, step); ys = np.interp(xs, sx, sy)
    out = []
    for x, y in zip(xs, ys):
        e, s = col_edge(x, y, win); out.append(e if s > 8 else y)
    return xs, smooth(out)

def ribbon(name, xs, ys, lift, lane, material, note=""):
    return dict(name=name, lane=lane, material=material, note=note, points=[[round(float(x), 2), round(float(y - lift), 2)] for x, y in zip(xs, ys)])

R = []
# seeds = rough marble/face edge from the 2x crops; the tracer then snaps them to the real edge
top = [(398, 611), (420, 610), (500, 622), (600, 638), (700, 651), (800, 657), (900, 653), (1000, 643), (1090, 635), (1118, 632)]
low = [(338, 655), (420, 659), (500, 672), (600, 689), (700, 701), (800, 706), (900, 701), (1000, 690), (1090, 680), (1118, 677)]
for nm, seed, lane, note in (("dais top tier lip", top, "M", "walk along the marble band in front of the bath"), ("dais lower tier lip", low, "M", "the steps down to the floor")):
    xs, ys = trace(seed); R.append(ribbon(nm, xs, ys, 6.0, lane, "marble", note))
# floor lane: the base of the dais (dark face meets bright floor, measured ~738 at 600-1000) + 14px; carried on to the stool (legs end ~750-760)
fx = [560, 700, 800, 900, 1000, 1100, 1200, 1300]; fy = [751, 753, 752, 752, 751, 753, 758, 762]
xs = np.arange(560, 1301, 8.0); R.append(ribbon("floor lane", xs, smooth(np.interp(xs, fx, fy)), 0.0, "F", "tile", "kittens cross the floor to the stool"))
# stool top: closed ellipse (picture px) measured from the 4x crop: back edge y 664, front piping y 692
STOOL = dict(cx=1228.0, cy=678.0, a=76.0, b=14.0)
PLANTER = dict(x=500, y=520, note="kittens drop from the planter mouth")
json.dump(dict(picture="bathroom_level3_dollhouse.webp", size=[W, H], ribbons=R, stool_top=STOOL, planter=PLANTER, mum=dict(x=1228, y=678, on="stool top")), open(os.path.join(HERE, "level3_trace.json"), "w"), indent=1)

rev = img.copy(); d = ImageDraw.Draw(rev)
cols = [(0, 255, 255), (255, 255, 0), (0, 255, 0), (255, 0, 255)]
for i, r in enumerate(R):
    p = [tuple(q) for q in r["points"]]; d.line(p, fill=cols[i % 4], width=2)
    for q in p[::2]: d.ellipse([q[0] - 2, q[1] - 2, q[0] + 2, q[1] + 2], outline=cols[i % 4])
import math
d.line([(STOOL["cx"] + STOOL["a"] * math.cos(t / 30), STOOL["cy"] + STOOL["b"] * math.sin(t / 30)) for t in range(0, 189)], fill=(255, 255, 255), width=2)
d.ellipse([PLANTER["x"] - 6, PLANTER["y"] - 6, PLANTER["x"] + 6, PLANTER["y"] + 6], outline=(255, 0, 0), width=3); d.text((PLANTER["x"] + 10, PLANTER["y"] - 6), "START (planter)", fill=(255, 255, 255))
d.text((STOOL["cx"] - 20, STOOL["cy"] - 30), "MUM (stool top)", fill=(255, 255, 255))
rev.save(os.path.join(HERE, "level3_trace_review.png"))
rev.crop((330, 560, 1180, 760)).resize((1700, 400), Image.LANCZOS).save("/tmp/claude-0/-home-user/aa41efe8-b221-55ba-806c-ebb39c188a45/scratchpad/l3trace_crop.png")
for r in R: print(r["name"], r["points"][:3], "...", r["points"][-2:])
