# CATTAS 🐱

A cat-themed puzzle-platformer: guide a stream of cats safely home by giving them special **Cat Traits** — dig, climb, float, block, build and more.

Made with **Unity 6** (Universal 2D / URP).

## Status
**Step 1 – Grey-box prototype ✅**
Cats spawn from a hatch, walk, climb small bumps, turn at walls, fall (and splat if the drop is too high), and get saved at the exit. The ground is destructible pixel-by-pixel.

**Step 2 – Trait bar + Litter Lord ✅**
A bar of the 8 Cat Traits along the bottom (unbuilt ones greyed out). Pick a trait, then click a cat. The **Litter Lord** digs straight down through the ground. Test level: the exit is on the floor below, so you have to dig to reach it.

**Biscuit Baker ✅** – the cat kneads biscuits on the spot and other cats turn back. Test level now has a deadly pit under the ledge.

**Fluffchute ✅** – permanent; the cat puffs up after falling a little way, drifts down slowly and never splats.

**Zoomies ✅** – the cat charges forward smashing a tunnel through walls, and calms down when there's nothing left in front. Test level now has two routes: dig down, or zoom through the wall to a gap on the right.

## How to run
1. Open the project in Unity 6.
2. Open any scene (e.g. `Assets/Scenes/SampleScene`) and press **Play**.
   The game builds itself at runtime via `GameBootstrap` — no scene setup needed.

## Code layout
| File | What it does |
|---|---|
| `Assets/Scripts/GameBootstrap.cs` | Starts the game, builds the test level, spawns cats, runs the 30-tick/sec game loop, draws the HUD |
| `Assets/Scripts/Cat.cs` | One cat's behaviour (walking, falling, digging, landing, exit) |
| `Assets/Scripts/CatTraits.cs` | The list of Cat Traits and which ones are built |
| `Assets/Scripts/DiggableGround.cs` | Pixel-based destructible terrain |
| `Assets/Scripts/SpriteMaker.cs` | Generates placeholder sprites from code |

See [`docs/DESIGN.md`](docs/DESIGN.md) for the full game design and roadmap.
