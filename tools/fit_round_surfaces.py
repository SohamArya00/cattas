"""Measures the round surfaces of the Level 2 bathroom picture to sub-pixel accuracy.

For each round object we start from a rough ellipse, look along its outward normal for the real edge in the pixels
(strongest brightness change), and least-squares fit an ellipse to the edge points found. The result (centre, radii,
RMS error in picture pixels) is what the physics will use, and a review picture shows the fitted curves on the art.
The picture itself is only READ here. Usage: python3 tools/fit_round_surfaces.py"""
import os, json
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(__file__)
SRC = os.path.join(HERE, "art_sources", "bathroom_level2_1960s.webp")
img = Image.open(SRC).convert("RGB")
A = np.asarray(img, np.float32)
L = A[..., 0] * 0.299 + A[..., 1] * 0.587 + A[..., 2] * 0.114
Hh, Ww = L.shape

def sample(x, y):                                    # bilinear brightness
    x = np.clip(x, 0, Ww - 1.001); y = np.clip(y, 0, Hh - 1.001)
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int); fx = x - x0; fy = y - y0
    return (L[y0, x0] * (1 - fx) * (1 - fy) + L[y0, x0 + 1] * fx * (1 - fy) +
            L[y0 + 1, x0] * (1 - fx) * fy + L[y0 + 1, x0 + 1] * fx * fy)

def fit_ellipse(pts):
    """axis-aligned ellipse: A x^2 + B y^2 + C x + D y = 1"""
    x, y = pts[:, 0], pts[:, 1]
    M = np.stack([x * x, y * y, x, y], 1)
    sol, *_ = np.linalg.lstsq(M, np.ones(len(x)), rcond=None)
    a_, b_, c_, d_ = sol
    cx, cy = -c_ / (2 * a_), -d_ / (2 * b_)
    f = 1 + a_ * cx * cx + b_ * cy * cy
    return cx, cy, np.sqrt(f / a_), np.sqrt(f / b_)

def resid(pts, e):
    cx, cy, a, b = e
    return np.abs(np.sqrt(((pts[:, 0] - cx) / a) ** 2 + ((pts[:, 1] - cy) / b) ** 2) - 1) * np.sqrt(a * b)

def find(seed, sign=-1, win=8.0, step=0.25, min_contrast=12, arc=None):
    """seed = (cx, cy, a, b). sign=-1: bright inside -> dark outside (strongest DROP going outward); +1: the opposite.
    arc = optional (from_deg, to_deg) to use only part of the outline. Returns (ellipse, edge points used, rms error)."""
    cx, cy, a, b = seed
    pts = []
    degs = np.arange(0, 360, 2.0) if arc is None else np.arange(arc[0], arc[1], 2.0)
    for th in np.radians(degs):
        ex, ey = cx + a * np.cos(th), cy + b * np.sin(th)
        nx, ny = np.cos(th) / a, np.sin(th) / b
        n = np.hypot(nx, ny); nx, ny = nx / n, ny / n
        t = np.arange(-win, win + step, step)
        prof = np.convolve(sample(ex + nx * t, ey + ny * t), np.ones(5) / 5, mode="same")
        g = np.gradient(prof, step)
        k = int(np.argmin(g)) if sign == -1 else int(np.argmax(g))
        if abs(g[k]) * 3.0 < min_contrast or k < 8 or k > len(t) - 9: continue     # no clear edge here
        pts.append((ex + nx * t[k], ey + ny * t[k]))
    if len(pts) < 12: raise RuntimeError(f"only {len(pts)} clear edge points found")
    pts = np.array(pts)
    e = fit_ellipse(pts)
    for _ in range(4):                                    # throw away outliers and refit
        r = resid(pts, e); keep = r < max(1.2, 2.5 * np.median(r))
        if keep.sum() < 12 or keep.all(): break
        pts = pts[keep]; e = fit_ellipse(pts)
    return e, pts, float(np.sqrt(np.mean(resid(pts, e) ** 2)))

targets = {   # name: (rough seed, sign, window px, arc)
    "tub_rim_outer":    ((762, 525, 216, 19), -1, 8, None),
    "sink_rim_outer":   ((1289, 499, 97, 6), -1, 6, None),
    "stool_top":        ((1156, 692, 76, 11), -1, 7, None),
    "lamp_left_globe":  ((625, 146, 85, 72), -1, 10, (-170, 170)),
    "lamp_right_globe": ((1407, 122, 52, 52), -1, 10, (-170, 170)),
}
results, overlay = {}, img.copy()
od = ImageDraw.Draw(overlay)
colours = [(0, 255, 255), (255, 255, 0), (0, 255, 0), (255, 0, 255), (255, 255, 255), (0, 128, 255)]
for i, (name, (seed, sign, win, arc)) in enumerate(targets.items()):
    try:
        e, pts, rms = find(seed, sign, win, arc=arc)
    except Exception as ex:
        print(name, "FAILED", ex); continue
    cx, cy, a, b = e
    results[name] = dict(cx=round(cx, 2), cy=round(cy, 2), a=round(a, 2), b=round(b, 2), rms=round(rms, 2), points=int(len(pts)))
    print(f"{name:18s} centre ({cx:7.2f},{cy:7.2f})  radii a={a:6.2f} b={b:6.2f}   rms {rms:4.2f}px  from {len(pts)} edge points")
    col = colours[i % len(colours)]
    th = np.radians(np.arange(0, 360, 1.0))
    od.line([(cx + a * np.cos(t), cy + b * np.sin(t)) for t in th] + [(cx + a, cy)], fill=col, width=1)
    for p in pts[::3]: od.ellipse([p[0] - 1, p[1] - 1, p[0] + 1, p[1] + 1], fill=col)
json.dump(results, open(os.path.join(HERE, "round_surfaces_fit.json"), "w"), indent=1)
overlay.save(os.path.join(HERE, "round_surfaces_fit_review.png"))
