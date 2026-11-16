# Level 2 – The 1960s Bathroom (second picture): surface map and physics notes
**Status: proposal for discussion. Nothing here is built yet, and the artwork is untouched.**
Source picture: `tools/art_sources/bathroom_level2_1960s.webp` (1672×941). Review map: `tools/level2_map_review.png` (made by `tools/level2_map_review.py`).

## Materials (proposal – Codi to confirm)
| Material | Digging | Examples |
|---|---|---|
| **Soft** | can be dug | timber (cabinet, shelves, frames), cushions, rug, plants/leaves, vases (Codi's call), towels |
| **Hard** | can't be dug | marble (dais, counter), porcelain (tub, toilet, sink), brass (taps, legs, rail, sconces, clock), glass (window, mirror), floor tiles |
| **Decoration** | not solid | hanging vines, flowers, painted pictures, recessed lights, cords |
Unsure: ceramic plant pots, gold wall shelves (metal or timber?), the window, the black cat statue.

## Depth lanes (so kittens can walk in front of or behind things)
- **FRONT lane:** floor, rug, stool and its legs.
- **ROOM lane:** dais tiers, tub, taps, plant pot, toilet, vanity, sink, niche, wall shelves, frames, mirror, sconces, window sill, grab bar.
- A kitten only collides with things in its own lane. It changes lane by **landing** on, or **stepping up** onto (low edges only), something from another lane. Tall things in another lane are simply walked past.

## Walkable surfaces (picture y; height above the floor lane y=785, in level pixels = picture px ÷ 2)
| # | Surface | y | height | batches of hairballs to climb* |
|---|---|---|---|---|
| 1 | Floor / rug | 785 | 0 | – |
| 21 | Stool seat (soft) | ~685 | ~50 | 2 |
| 2 | Dais lower tier (marble) | 706 | 40 | 2 |
| 3 | Dais top tier (marble) | 650 | 68 | 3 |
| 19 | Vanity counter (marble) | 556 | 115 | 5 |
| 4 | Tub rim (porcelain) | 526 | 130 | 6 |
| 5 | Toilet tank lid | 497 | 144 | 6 |
| 20 | Sink rim | 497 | 144 | 6 |
| 7 | Brass grab bar (thin) | 490 | 148 | 6 |
| 11 | Window sill / frame | 488 | 149 | 7 |
| 15–18 | Niche shelves (timber) | 437 / 524 / 609 / 694 | 174 / 130 / 88 / 46 | 8 / 6 / 4 / 2 |
| 13, 12 | Right gold shelves | 356 / 266 | 215 / 260 | 9 / 11 |
| 9, 8 | Left shelves (timber) | 329 / 168 | 228 / 308 | 10 / 13 |
| 10 | Cat-picture frame top | 257 | 264 | 11 |
| 14 | Portrait frame top | 58 | 364 | 15 |
\* One Hairball Bridge batch = 12 hairballs = +24 up, +48 forward. **Climbing a height h needs a run of 2h**, so the room's 836px width limits how high a bridge can go even with 100 bridges, and the bridge stops at anything solid in the way.

## Notes per object
- **Tub:** hard. Walk over its rim; brass taps (hard, ~45px tall) must be bridged over; the vase at the right end is soft, dig through it. The tub interior isn't visible in the picture, so it is solid for now.
- **Dais:** marble, so no digging down; it is the route between the floor and the tub area.
- **Toilet, sink:** porcelain, hard; their tops are walkable platforms.
- **Vanity:** marble counter (walkable), timber cabinet (diggable: dig into it), brass legs/handles hard.
- **Wall things:** shelves, frames, mirror top, sconce tops are walkable ledges in the ROOM lane; vines are soft and climbable (NG+).
- **Hanging lamps, clock, window:** hard; mostly NG+ secrets.
