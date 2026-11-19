# "Silk" engine - plan for the Doll House test map (draft for Codi)

Goal: movement that looks hand-animated and 4K-clean, with true 2.5D depth. Levels 1-2 stay as they are; Silk is built
beside them, so nothing already working can break. The Doll House picture is saved untouched as
`tools/art_sources/bathroom_level3_dollhouse.webp`.

## What I measured in your picture
- Floor tile edges slope about 12-15 degrees on BOTH sides, mirror-imaged: lines rise towards the middle of the picture on the
  left half and on the right half. So the room really is a corner/doll-house view and the "centre line" is where the two sides meet
  (around the right edge of the window, x about 1000 of 1672). I will fit the exact line and slopes from the tile edges, not guess.
- Your red route: planter (left, upper dais) -> along the top tier in front of the bath -> down the dais steps -> floor -> stool/vanity corner.

## The seven pillars
1. **Fixed 60 Hz simulation, interpolated drawing.** The sim runs a steady 60 steps a second (was 30) and the screen shows
   positions blended between steps, so 144 Hz and 4K monitors never judder. Kitten positions are decimals (1/16 pixel), not whole pixels.
2. **Finer grid.** 1 level pixel = 1 picture pixel (4x more cells than Level 2). Digging, hairballs and holes are drawn at that detail.
3. **Walkways are smooth curves, not pixel staircases.** Each surface (top tier lip, steps, floor, stool top...) is traced as a smooth
   spline "ribbon" with its own depth lane. Kittens move along the ribbon by arc length, so slopes and curves are silky.
   Digging/stairs are a pixel layer laid over the ribbons (a hole cuts the ribbon, a hairball adds to it).
4. **Perspective speed and size.** Every point of the level has a depth scale (a small grid of numbers fitted to the picture).
   Kittens are drawn bigger and move faster on screen near the left/right edges and bottom, smaller and slower towards the centre line,
   and they face "towards" or "away from" the screen on the tilted floor lines. World speed stays constant, so timing is fair.
5. **Depth lanes stay.** Back / mid / front lanes (as in Level 2) give proper passing in front of and behind objects, plus an occlusion
   mask so a kitten walking behind the bath rim is hidden by it. Needs no art changes.
6. **Pixel-exact tracing pipeline.** I trace every surface from your picture with an edge-snapping tool (finds the real lip of each
   marble edge to sub-pixel accuracy), draw all outlines over the picture, and you review/approve before it goes in the game.
7. **Soft contact.** Squash on landing, a tiny anticipation before jumps/step-ups, shadows that follow the ribbon, rim-light tint from
   the nearest lamp. This is where "smooth as silk" is felt. (Pure code, no new art.)

## Level brief (as I understand it)
20 kittens drop from the circled planter, walk the top tier in front of the bath, go down the steps, cross the floor, and a fur
staircase gets them up to Mum. Budget: 5 Hairball Bridge uses, 5 Biscuit Baker blockers, 120 seconds, 80% (16 of 20) must reach Mum.

## Build order
M1 trace + overlay review (you approve) -> M2 ribbons + 60 Hz + perspective scale, kittens walking the route -> M3 steps, stool, Mum
-> M4 hairball stairs + blockers on the new engine -> M5 polish (squash, shadows, occlusion) and a speed/difficulty tune with the simulator.
