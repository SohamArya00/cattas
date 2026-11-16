# Level 2 physics spec (proposal – nothing here is built yet)
Test-bed picture: `tools/art_sources/bathroom_level2_1960s.webp` (Codi allows amending *this* picture; keep the original).

## 1. Measured geometry (picture pixels, y down) – from `tools/fit_round_surfaces.py`
Automatic edge-fitting: look along each curve's normal for the real edge in the pixels, least-squares ellipse.
| Object | centre (x, y) | radii (a across, b up/down) | RMS error | notes |
|---|---|---|---|---|
| Tub rim, outer edge | (772.8, 524.0) | 214.1 × 16.5 | 0.98 px | very flat ellipse (b/a = 0.077); ends slightly wider than a perfect ellipse |
| Left pendant globe | (624.1, 148.8) | 83.8 × 73.9 | 0.26 px | oblate; gold bands at y ≈ 125–135 and 144–162 |
| Right pendant globe | (1408.1, 124.2) | 52.6 × 51.4 | 0.56 px | near circle |
| Sink rim | hand-measured (1289, 497) | ≈ 96 × 3 | ±2 px | too faint to auto-fit |
| Stool top | hand-measured (1156, 692) | ≈ 76 × 11 | ±2 px | too faint to auto-fit |
Not measured yet: toilet lid/seat, dais tier arcs, plant pot rims, vases.
Hint for the artist: crisp, high-contrast edges and true ellipses (ellipse tool) fit to <1 px automatically.

## 2. Surface paths ("walk-around" loops)
- A **path** is an ellipse (closed loop or open arc) with a material and a depth rule. A kitten on a path moves along it at 1 px/tick of arc length, so it runs round the ends, even where the curve is too steep to walk on a flat grid.
- **Closed loops** (tub rim, toilet lid, sink rim, stool top, lamp bands): the kitten goes round until something stops it (a hard object on the rim such as the taps turns it back; Biscuit Baker; a trait).
- **Open arcs** (dais tiers, half circles): the kitten follows the arc and turns back at the ends/walls.
- **Entering:** landing on a path from above, or stepping up onto it from a surface it touches (e.g. the top of a bridge). **Leaving:** falling off an end, digging, or a trait.
- **Depth tricks (rendering only):** on the back half of a loop the kitten is drawn ~6% smaller, slightly darker and behind the front rim; on the front half full size and in front. A soft contact shadow sits under the kitten.

## 3. Hanging lights (spheres)
- A sphere is a circle with a slope rule: a kitten on its curved surface slides off where the slope passes ~45° (before the gold bands, which are ~90°).
- Globes are **diggable**: a kitten that digs in from the top falls into the hollow interior (concave bowl) and must dig out.

## 4. Materials
- Everything is diggable **except** the hard list: marble, porcelain, brass, glass (window, mirror), floor tiles **and the rug**.
- Hanging lights are diggable (exception to "glass is hard").

## 5. Curtain Climber limits (proposal)
Soft surfaces only; slips on hard ones. Maximum climb of ~80 level px per climb (about 8 kitten heights), then it lets go and falls. Cannot climb overhangs.

## 6. Resolution
The physics grid is 1 level px = 2 picture px. For 4K-safe detail we can double it (1 px = 1 picture px, all constants ×2; 4× the memory, fine). Rails use exact curve geometry either way.
