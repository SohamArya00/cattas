# CATTAS 🐱

A cat-themed puzzle-platformer: guide a stream of cats safely home by giving them special **Cat Traits** — dig, climb, float, block, build and more.

Made with **Unity 6** (Universal 2D / URP).

## Status
**Kitchen demo ✅** – big painted kitchen (960×540) with zoom/scroll camera, minimap, look-around preview, hazards (full sink, gas hob, metal fridge), a gas valve switch, knock-over props hiding 3 secrets, goals/trophies, animated kittens, particles and sound effects.

**Levels ✅** – level select with saved progress, timer, win/lose screens, scrolling camera, stone blocks, and 10 starter levels that introduce the traits one by one.

**Step 1 – Grey-box prototype ✅**
Cats spawn from a hatch, walk, climb small bumps, turn at walls, fall (and splat if the drop is too high), and get saved at the exit. The ground is destructible pixel-by-pixel.

**Step 2 – Trait bar + Litter Lord ✅**
A bar of the 8 Cat Traits along the bottom (unbuilt ones greyed out). Pick a trait, then click a cat. The **Litter Lord** digs straight down through the ground. Test level: the exit is on the floor below, so you have to dig to reach it.

**Biscuit Baker ✅** – the cat kneads biscuits on the spot and other cats turn back. Test level now has a deadly pit under the ledge.

**Fluffchute ✅** – permanent; the cat puffs up after falling a little way, drifts down slowly and never splats.

**Zoomies ✅** – the cat charges forward smashing a tunnel through walls, and calms down when there's nothing left in front. Test level now has two routes: dig down, or zoom through the wall to a gap on the right.

**Curtain Climber ✅** – permanent; next time the cat meets a wall it climbs straight up and over. Bonks its head on ceilings and falls back. Combine with Fluffchute to survive the drop on the other side.

**Cable Chewer ✅** – 5-second countdown (number above the cat, sparks), then ZAP: blasts a round hole and the frizzy cat shoots off home. Works on Biscuit Bakers too.

**Hairball Bridge ✅** – hacks up 12 hairballs, one a second, each a step up and forward: a staircase bridge across gaps.

**Scratcher ✅** – claws a sloping tunnel diagonally down and forwards until it breaks out into open air.

🎉 **All 8 Cat Traits are in!**

## How to run
1. Open the project in Unity 6.
2. Open any scene (e.g. `Assets/Scenes/SampleScene`) and press **Play**.
   The game builds itself at runtime via `GameBootstrap` — no scene setup needed.
3. Pick a level from the level select screen. Wide levels scroll with **arrow keys / A D** or **right-click drag**.

## Art pipeline (for artists)
Painted art lives in `Assets/Resources/Art/`. Save images as **PNG, then rename the file to end in `.bytes`** (e.g. `kitchen_bg.png` → `kitchen_bg.bytes`) so Unity keeps them full-size and uncompressed.

| File | What it is | Size |
|---|---|---|
| `<name>_bg` | Background picture behind everything | 2560×1440 (4× the 640×360 level) |
| `<name>_fg` | The solid ground/furniture. **Transparent = air**; anything opaque is walkable/diggable | 2560×1440, PNG with transparency |
| `cat_sheet` | Kitten animation: 6 frames side by side, each 48×40 — 4 walk frames, 1 falling, 1 sitting | 288×40 |

Hook them up in a level file with `background: <name>_bg`, `foreground: <name>_fg`, and `markers: off` (hides the plain hatch/exit boxes when the art draws its own). In painted levels the text map only marks `H` (hatch), `E` (exit) and `X` (stone that can't be dug).
Placeholder art is drawn by `tools/make_kitchen_art.py`; previews are in `tools/*_preview.png`.

## Making levels
Levels are plain text files in `Assets/Resources/Levels/` (`level01.txt`, `level02.txt`, ...) — open them in Notepad!
The top has settings, then `map:` and a picture of the level. Each character is an 8×8 pixel block:

| Char | Meaning |
|---|---|
| `.` | air |
| `#` | dirt (can be dug) |
| `X` | stone (can't be dug, zoomed or zapped) |
| `/` `\` | dirt ramps |
| `H` | hatch – cats drop out of here |
| `E` | exit – put ground under it |

Settings: `name`, `hint`, `size` (e.g. `960 540`, for painted levels), `par` (target seconds for the speed-run goal), `cats`, `need`, `rate` (seconds between cats), `time` (seconds), `traits` (e.g. `LitterLord=3, Fluffchute=2`), and optional `speed` (starting game speed in %, 50 = normal; players can only speed up from there).
Maps are normally 45 rows tall; make them wider than 80 columns for a scrolling level.

**Objects** (one per line, game pixels with y measured up from the bottom):
```
object: water  x0 y0 x1 y1                 kittens that fall in get soaked
object: fire   x0 y0 x1 y1 gas             singes kittens while lit
object: switch x0 y0 x1 y1 gas valve       a kitten touching it turns that group off
object: prop   x0 y0 x1 y1 prop_pan        can be knocked over (secrets hide behind props)
object: secret x0 y0 x1 y1 secret_mouse Lost Toy Mouse
```

Handy numbers: cats can step up 6px (less than one block — use ramps!), and falls over 60px (more than 7 blocks) splat without a Fluffchute.

`bash tools/check_compile.sh` compile-checks the scripts without Unity. `tools/make_levels.py` generated the starter levels, and `tools/sim.py` is a copy of the cat rules in Python that checks each level is solvable (`python3 tools/sim.py`).

## Code layout
| File | What it does |
|---|---|
| `Assets/Scripts/GameBootstrap.cs` | Starts the game: level select, building levels, spawning cats, the 30-tick/sec game loop, camera, HUD |
| `Assets/Scripts/LevelData.cs` | Reads level text files |
| `Assets/Scripts/Cat.cs` | One cat's behaviour (walking, falling, digging, landing, exit) |
| `Assets/Scripts/CatTraits.cs` | The list of Cat Traits and which ones are built |
| `Assets/Scripts/DiggableGround.cs` | Pixel-based destructible terrain |
| `Assets/Scripts/SpriteMaker.cs` | Generates placeholder sprites from code |
| `Assets/Scripts/Effects.cs` | Simple fade-out effects (ZAP flash, frizzy cat) |
| `Assets/Scripts/Particle.cs` | Dust, debris and sparkle particles |
| `Assets/Scripts/Sfx.cs` | Placeholder sound effects generated in code |
| `Assets/Scripts/Art.cs` | Loads painted art and sprite sheets; rounded UI boxes |

See [`docs/DESIGN.md`](docs/DESIGN.md) for the full game design and roadmap.
