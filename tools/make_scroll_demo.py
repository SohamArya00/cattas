"""Slices the ChatGPT three-panel composite (tools/art_sources/threepanel_composite_v2.webp, untouched) into LEFT | MIDDLE | RIGHT screens
for the Scroll Lab demo. The pictures are only cut apart (white divider lines removed), never repainted.
Writes Assets/Resources/Art/demo_left|mid|right.bytes and Assets/Resources/Levels/scroll_demo.txt"""
import os
from PIL import Image
HERE = os.path.dirname(__file__)
ART = os.path.join(HERE, "..", "Assets", "Resources", "Art"); LEVELS = os.path.join(HERE, "..", "Assets", "Resources", "Levels")
im = Image.open(os.path.join(HERE, "art_sources", "threepanel_composite_v2.webp")).convert("RGB")
H = 369
left = im.crop((0, 381, 999, 381 + H)); right = im.crop((1004, 381, 2000, 381 + H)); mid = im.crop((0, 0, 2000, H))
for name, p in (("demo_left", left), ("demo_mid", mid), ("demo_right", right)):
    p.save(os.path.join(ART, name + ".bytes"), format="PNG", optimize=True)
widths = [left.width, mid.width, right.width]
FLOOR = 346          # where a kitten's feet stand: just in front of the wall/floor line (~333) on the floor band
total = sum(widths)
pts = [(x, FLOOR, 1.0, 1.0) for x in range(24, total - 23, 40)]
txt = f"""// Scroll Lab DEMO: the ChatGPT left | back | right pictures joined, flat head-on (placeholder art, sliced from the composite)
size {H}
screen demo_left {widths[0]}
screen demo_mid {widths[1]}
screen demo_right {widths[2]}
kitten 1.5
speed 60
count 8
spawn_gap 1.6
""" + "\n".join(f"r {x:.1f} {y:.2f} {s:.3f} {f:.3f}" for x, y, s, f in pts) + "\n"
open(os.path.join(LEVELS, "scroll_demo.txt"), "w", newline="\n").write(txt)
print(widths, total, len(pts), "route points")
