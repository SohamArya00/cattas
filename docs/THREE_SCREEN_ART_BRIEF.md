# Three-screen level: art brief (draft)

One level = three pictures joined side by side, scrolled left to right. No folding, no 3D: each screen is a normal picture.

| Screen | View | Perspective |
|---|---|---|
| 1 LEFT | wall at about 45 degrees | biggest at the far LEFT edge, shrinking towards the corner on the RIGHT edge |
| 2 MIDDLE | flat, head-on | no change across the screen |
| 3 RIGHT | mirror of screen 1 | smallest at the corner on the LEFT edge, growing to the far RIGHT edge |

Rules so the three join with no jump (see `tools/scroll_template_1..3.png`, a construction guide drawn by code):
1. Every screen is the same size (16:9; 3840 x 2160 for the final, 1920 x 1080 for concepts).
2. **Seams:** the RIGHT edge of screen 1 must match the LEFT edge of screen 2 pixel-for-pixel in height, floor line and tile size
   (same for screen 2 -> 3). Show a pale corner column at each seam.
3. **Horizon** (eye level) at about 39% from the top on all three. The wall/floor line starts at about 72% from the top at the seams and the whole middle screen.
4. **Scale:** things at the far outer edges of screens 1 and 3 are about 1.55 times the size of things at the seam. Wall tile width on the 45-degree walls is squashed to about 72% of the flat tile width.
5. **Kitten size** is 41 px tall at the seams on a 1080-high screen (62 px at the far left/right edge). Every walkable edge (shelf top, rim, step) needs a crisp, high-contrast outline.
6. Floor visible: about 28% of the height below the wall/floor line, with tile lines running to the vanishing point.

## Prompt for ChatGPT (attach the three template pictures and a reference of the retro bathroom style)
```
Paint three matching background pictures of the SAME 1960s Palm Springs bathroom in the attached style, following the three attached construction templates exactly. 
Picture 1 is the LEFT wall seen at about 45 degrees: biggest at the far left, shrinking towards the corner at the right edge. Picture 2 is the BACK wall seen flat, head-on. Picture 3 is the RIGHT wall at about 45 degrees (mirror of picture 1): small at the left edge corner, growing towards the right edge. 
Keep the horizon, wall/floor line, ceiling line and tile sizes exactly as drawn in the templates, and make the green edge columns identical where picture 1 meets 2, and 2 meets 3. 
Put a toilet on a low platform and a towel rail on the left wall; a stepped dais with a bath and a window on the back wall; a vanity, mirror and tall cabinet on the right wall. 
Crisp, clean outlines on every shelf, step, rim and counter top because tiny cats will walk on them. No cats, no people, no text.
```
