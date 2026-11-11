# CATTAS 🐱

A cat-themed puzzle-platformer: guide a stream of cats safely home by giving them special **Cat Traits** — dig, climb, float, block, build and more.

Made with **Unity 6** (Universal 2D / URP).

## Status
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

Settings: `name`, `hint`, `cats`, `need`, `rate` (seconds between cats), `time` (seconds), `traits` (e.g. `LitterLord=3, Fluffchute=2`), and optional `speed` (starting game speed in %, 50 = normal; players can only speed up from there).
Maps are normally 45 rows tall; make them wider than 80 columns for a scrolling level.

Handy numbers: cats can step up 6px (less than one block — use ramps!), and falls over 60px (more than 7 blocks) splat without a Fluffchute.

`tools/make_levels.py` generated the starter levels, and `tools/sim.py` is a copy of the cat rules in Python that checks each level is solvable (`python3 tools/sim.py`).

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

See [`docs/DESIGN.md`](docs/DESIGN.md) for the full game design and roadmap.
