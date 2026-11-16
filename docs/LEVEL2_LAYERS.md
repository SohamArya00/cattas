# Level 2 layers and surfaces

Three invisible depth lanes sit over the untouched picture. A kitten stands on a surface in ANY lane (`lanejump: on`) and
then adopts that lane, so it can walk from surface to surface.

| Lane | Colour in collision file | What lives there |
|---|---|---|
| Front (F) | green | floor (y>=785 px), stool cushion and legs |
| Mid (M) | blue | dais tiers, tub body, plant pot, toilet, vanity counter/cabinet/legs, sink bowl |
| Back (B) | red | taps, tub vase, sink faucet, vanity vase, window sill, grab bar, shelves, frame tops, sconce tops |
| Rim ring | M + B | tub rim: one loop path, kittens circle it for ever |

Moves between surfaces: fall or step off any edge onto the next surface below; Hairball Bridge builds fur stairs (the
other kittens now step onto them off the rim); Zoomies opens the rim's back edge; Curtain Climber (max 100px) scales soft walls.
Fluffchute makes drops over 60px safe. Kittens that run onto an unfinished bridge fall through the gap: wait or use a chute.
Still to do: toilet lid, sink rim, stool top, dais arcs, lamp spheres.
