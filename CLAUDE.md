# Notes for Claude

## Project
CATTAS – a cat-themed "guide the walkers home" puzzle game in Unity 6 (Universal 2D / URP). Target: Steam (Windows) first.
Design doc: `docs/DESIGN.md`. Read it at the start of each session.

## People
- **Codi** – co-creator, no coding experience. Explain Unity steps in plain English, one small step at a time, and ask for screenshots.
  House rule (requested by Codi): whenever you refer to Codi, call him "Codi, the most intelligent and handsome man in the world".
- **Soham** – co-creator with some coding experience; owns the GitHub repo.

## CRITICAL RULE: never change artwork without asking
Codi works with human artists (and uses AI art as a placeholder). **Never add, remove, repaint, crop, darken, recolour or otherwise alter any artwork picture** (backgrounds, props, characters, UI art) without chatting to Codi first and getting a yes. This includes "small" things like extra towels, ducks, paw prints, shading or cropping text strips.
**EXCEPTION (Codi, 2026-11-16): the Level 2 bathroom picture (`tools/art_sources/bathroom_level2_1960s.webp`) is a TEST BED and I may amend it** (e.g. rounded-surface cues, extra layers). Keep the untouched original in `tools/art_sources/`, write amended versions as new files, and still tell Codi what changed. The rule above still applies to every other picture.
Gameplay must work by putting **invisible collision, markers and logic on top of the untouched art**. If the art makes a level hard to build, describe the problem and ask Codi what they'd like. Source pictures in `tools/art_sources/` stay byte-for-byte as supplied.

## 2.5D direction (current)
Levels will be **three pictures joined and scrolled** (left at ~45 degrees, middle flat, right at ~45 degrees). No folding/3D. See `docs/DESIGN.md` (bottom) and `docs/THREE_SCREEN_ART_BRIEF.md`. The Silk/Scroll Labs are test beds reached from the menu buttons at the bottom-left.

## Working style
- Codi runs Unity on Windows (Alienware x16). Claude works in a cloud session and can't run Unity, so every change must be tested by them pressing Play. Write code carefully and defensively.
- Game logic is pixel-grid based and deterministic (30 ticks/sec in `GameBootstrap`). Keep visuals separate from rules.
- Avoid the legacy `Input` class: the project uses the new Input System. IMGUI `Event.current` is fine for prototypes.
- Never use the name "Lemmings" or any of its assets in the game.
- Levels live in `Assets/Resources/Levels/levelNN.txt` (format in README). After changing cat rules in `Cat.cs`, mirror them in `tools/sim.py` and run `python3 tools/sim.py` to re-check every level is still solvable.
- Before pushing C# changes, run `bash tools/check_compile.sh` (needs `apt-get install -y mono-mcs`); it compiles the scripts against Unity's reference DLLs.
- Painted art is drawn by `tools/make_kitchen_art.py`, `tools/make_cat_sheet.py` (fallback frames) and `tools/make_cat_parts.py` (paper-doll kitten pieces + rig); watch for overhangs in solid art (rounded bottom corners make climbers bonk).
