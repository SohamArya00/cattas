# CATTAS – Game Design Doc (living document)

## Pitch
A modern, cat-themed take on the classic "guide the walkers to the exit" puzzle genre. 2.5D, beautiful high-res art, desktop first (Steam), then phone / handheld.

> Legal note: the genre and mechanics are free to use, but we never use the name "Lemmings", its art, levels, music or sounds. All content is original.

## Core loop
Cats drop from a hatch and walk forward mindlessly. The player assigns **Cat Traits** to individual cats to reshape the level and get enough cats home before time runs out.

## Cat Traits
| Classic skill | Cat Trait | Behaviour |
|---|---|---|
| Digger | **Scratcher** | Digs straight down |
| Floater | **Fluffchute** | Puffs up fur, survives any fall |
| Blocker | **Loaf** | Sits as a cat loaf; others turn around |
| Miner | **Burrower** | Digs diagonally down |
| Climber | **Clawber** | Climbs vertical walls |
| Basher | **Headbutter** | Tunnels horizontally through a wall |
| Bomber | **Hissy Fit** | 5s countdown, then POOF – blasts terrain; cat zips home dizzy (not counted as saved) |
| Builder | **Yarn Bridge** | Knits a short diagonal bridge |

## Progression
- **100 levels.** Levels 1–10 are gentle and unlock one trait at a time.
- Each level has a fixed trait allowance; levels should have several solutions.
- **Every 10th level:** a boss / special hazard to work around.
- **Finishing level 100** unlocks **NG+**: all traits available on all levels.
  - NG+1: +5 of every trait on top of the base allowance. NG+2: +10, etc.
  - **Golden Trait** on every 10th level in NG+ only: usable once in NG+1, twice in NG+2, etc.
- **NG+2** unlocks the **Level Builder** (community levels – details TBD; paid UGC is a later, legally-reviewed feature).

## Objectives & rewards (per level)
- Complete (save the required number)
- Save 100% of cats
- Speed run (beat a target time)
- Use fewest traits
- Find the hidden Easter egg / secret cat
Rewards: trophies, cosmetics, bonus levels.

## Cosmetics
Easter eggs and side quests unlock cat appearances: coat colours, hats, and full transformations (dinosaurs, dragons, ...). Fully customisable cattas.
(Joke idea parked: a "drunk lads walking home from the pub" skin pack.)

## Home hub
A cosy house where trophies, unlocked skins and collectibles are displayed.

## Tech approach
- **Gameplay** runs on a 2D pixel grid (destructible terrain, deterministic 30 ticks/sec).
- **Visuals** are a separate layer on top: 3D/2.5D cats, layered parallax backgrounds, lighting, particles. Art can be swapped without touching game rules.
- Scrollable / zoomable large levels (mouse + touch).

## Team
- Codi – design & production
- Soham – code
- Artist – TBD name
- Music & SFX – TBD name

## Roadmap
- [x] Step 0 – Unity installed, project created
- [x] Step 1 – Grey-box: walking, falling, splat, exit, destructible ground
- [x] Step 2 – Trait bar UI + Scratcher
- [ ] Step 3 – Remaining 7 traits
- [ ] Step 4 – Level data format + first 10 levels, timer, win/lose screens
- [ ] Step 5 – Scrolling/zoom camera, menus, save data
- [ ] Demo build + Steam "Coming Soon" page (target: Christmas 2026)
- [ ] Art & audio integration
- [ ] Levels 11–100, NG+, cosmetics, home hub
- [ ] Phone / handheld port
- [ ] Level builder
