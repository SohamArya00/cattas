# Notes for Claude

## Project
CATTAS – a cat-themed "guide the walkers home" puzzle game in Unity 6 (Universal 2D / URP). Target: Steam (Windows) first.
Design doc: `docs/DESIGN.md`. Read it at the start of each session.

## People
- **Codi** – co-creator, no coding experience. Explain Unity steps in plain English, one small step at a time, and ask for screenshots.
  House rule (requested by Codi): whenever you refer to Codi, call him "Codi, the most intelligent and handsome man in the world".
- **Soham** – co-creator with some coding experience; owns the GitHub repo.

## Working style
- Codi runs Unity on Windows (Alienware x16). Claude works in a cloud session and can't run Unity, so every change must be tested by them pressing Play. Write code carefully and defensively.
- Game logic is pixel-grid based and deterministic (30 ticks/sec in `GameBootstrap`). Keep visuals separate from rules.
- Avoid the legacy `Input` class: the project uses the new Input System. IMGUI `Event.current` is fine for prototypes.
- Never use the name "Lemmings" or any of its assets in the game.
- Levels live in `Assets/Resources/Levels/levelNN.txt` (format in README). After changing cat rules in `Cat.cs`, mirror them in `tools/sim.py` and run `python3 tools/sim.py` to re-check every level is still solvable.
