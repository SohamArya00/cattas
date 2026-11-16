# Level 2 layers and surfaces (Codi's physics test bed)

Three invisible depth lanes sit over the untouched picture. A kitten is blocked only by walls in its own lane, but it can
stand on a surface from ANY lane and then moves into that lane. Curved things are either a **path** (an ellipse kittens run
round) or a **curved lip** (a painted collision outline that kittens simply walk along).

| Lane | What lives there |
|---|---|
| Front (F) | floor, stool (cushion, legs) |
| Mid (M) | lower dais tier, tub body, plant pot, toilet bowl + seat, vanity, sink bowl |
| Back (B) | upper dais tier (it sits BEHIND the lower one), taps, vases, window sill + 4 window bars, shelves, frame tops, sconces, tile top edge, hanging lights |

## Surfaces
| Surface | Kind | Notes |
|---|---|---|
| Tub rim | closed path (M front / B back) | loops for ever; taps + vase block the back edge |
| Toilet seat | closed path | back edge runs into the tank, so it ping-pongs |
| Sink rim | closed path | faucet blocks the back edge |
| Stool top | closed path (F) | reached by walking off the dais' right end |
| Dais lower tier / upper tier | curved lip | upper tier is only reachable by falling onto it (>60px from the rim: Fluffchute) |
| Hanging lights | open arc 45-135 deg, SLIDE | kittens land on the cap and slide off; soft globe (diggable), hard brass bottom ring |
| Shelves, window bars, frame tops, sconce tops, tile edge, sill, grab bar, vanity top | flat ledges | |

Not walkable on purpose: mirror (glass), window glass, rug, floor tiles can't be dug.

Moves between surfaces: step/fall off an edge onto whatever is below; Hairball Bridge (stairs, other kittens follow them);
Zoomies; Curtain Climber (soft walls only, max 100px); Fluffchute (drops over 60px).
Measurements are by eye from 2x crops (+-3 picture px). Still to do: gold bands on the lights, hollow light interior.
