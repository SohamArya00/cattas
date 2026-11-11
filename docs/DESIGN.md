# CATTAS – Game Design Doc (living document)

## Pitch
A modern, cat-themed take on the classic "guide the walkers to the exit" puzzle genre. 2.5D, beautiful high-res art, desktop first (Steam), then phone / handheld.

> Legal note: the genre and mechanics are free to use, but we never use the name "Lemmings", its art, levels, music or sounds. All content is original.

## Story
The Cattas are **your cat's kittens**, playfully trying to find their way back to you. Their adventure takes them through every room of the house and beyond. Finishing level 100 plays a closing cutscene: the game is complete (and NG+ opens).

## Core loop
Cats drop from a hatch and walk forward mindlessly. The player assigns **Cat Traits** to individual cats to reshape the level and get enough cats home before time runs out.

## Cat Traits
| Classic skill | Cat Trait | Behaviour |
|---|---|---|
| Digger | **Litter Lord** | Digs straight down, very seriously |
| Floater | **Fluffchute** | Puffs up fur, survives any fall |
| Blocker | **Biscuit Baker** | Kneads biscuits on the spot, turning round and round; other cats turn back |
| Miner | **Scratcher** | Claws diagonally down |
| Climber | **Curtain Climber** | Climbs vertical walls |
| Basher | **Zoomies** | Zooms straight through walls, smashing a tunnel |
| Bomber | **Cable Chewer** | Chews a cable for 5s, then ZAP! – blasts a hole; the frizzy cat shoots off home to recover (not counted as saved). Also frees Biscuit Bakers |
| Builder | **Hairball Bridge** | Hacks up 12 hairballs over 12 seconds, building a 12-step staircase bridge |

## Progression
- **100 levels.** Levels 1–10 are gentle and unlock one trait at a time.
- Each level has a fixed trait allowance; levels should have several solutions.
- **Every 10th level:** a boss to get past (see Bosses).
- **Finishing level 100** unlocks **NG+**: all traits available on all levels.
  - NG+1: +5 of every trait on top of the base allowance. NG+2: +10, etc.
  - **Golden Trait** on every 10th level in NG+ only: usable once in NG+1, twice in NG+2, etc.
- **Golden Traits only in NG+**, on every 10th level.
- **NG+2** unlocks the **Level Builder**: players design and share levels. Paying creators (e.g. per download, as DLC) is a goal, but needs legal/tax/moderation research and Steam's rules first – start with free sharing (Steam Workshop).

## Levels 1–10
| # | Name | Teaches |
|---|---|---|
| 1 | Dig In | Litter Lord |
| 2 | Soft Landing | Fluffchute |
| 3 | Knead to Know | Biscuit Baker |
| 4 | Zoom Zoom | Zoomies |
| 5 | Up the Curtains | Curtain Climber |
| 6 | Bzzzt! | Cable Chewer |
| 7 | Diagonal Thinking | Scratcher |
| 8 | Hairy Situation | Hairball Bridge (+ baker, + zap the baker free) |
| 9 | Athletes | Trait combos (climb + float) |
| 10 | Graduation Day | Everything, wide scrolling level |

## Worlds (where levels take place)
Levels are set in and around the home, seen at cat height:
bathroom, kitchen, living room, bedroom, laundry, backyard, garden shed, the car, the vet's...
Roughly 10 levels per area, with a boss at the end of each.

## Interactive objects
Each area has objects cats can trigger, opening extra solutions:
- Bar of soap – slippery, cats slide further
- TV remote – turns the TV on (light/distraction)
- Hose tap – starts the sprinkler
- (more per area: fridge door, washing machine, ceiling fan, toaster, garage door...)

## Bosses (every 10th level)
A **scruffy, funny-looking alley cat** guards the way (proposed: grumpy and comically scruffy rather than starving, so it stays light-hearted). Get the kittens past him by going around him, or by finding the secret that chases him off:
- Turn on the hose tap – the sprinkler soaks him and he runs off
- Let the neighbour's dog out
- Drop something on his tail / scare him off (cartoon-style, no real harm)
Different boss tricks per area.

## Golden Traits (NG+ only, every 10th level)
When used, the camera zooms in on the catta as it gets ready, then the move plays out.
| Golden Trait | Idea |
|---|---|
| **Number Two** 💩 | The big one, saved from early on |
| **9 Lives** | Comes back from anything |
| **Catta Copta** | Spins its tail like a helicopter and flies |
| **Catnip** | Goes wild with trippy vision – can see all the Easter eggs |
| **Furball** | Giant hairball (exact effect TBD) |
| **Teleport** | Pops from one spot to another |
| **Circus Cannon** | Fired through a flaming hoop |
| **Spicy Food** 🌶️ | Eats something spicy, flame-farts and flies around randomly |
| **Meme moves** | Topical/meme-inspired moves – original jokes only, no copyrighted memes |

## Objectives & rewards (per level)
- Complete (save the required number)
- Save 100% of cats
- Speed run (beat a target time)
- Use fewest traits
- Find the hidden Easter egg / secret cat
Rewards: trophies, cosmetics, bonus levels.

## Cosmetics & Catta creator
- Easter eggs and side quests unlock cat appearances: coat colours, hats, and full transformations (dinosaurs, dragons, ...). Fully customisable cattas.
- **Catta creator:** build your cat from several cat types/parts (breeds, coat patterns, eyes, accessories). The kittens are generated to match the parent; both shown on the profile screen.
- **Stretch goal – your own cat:** upload a photo of your real cat and have it turned into the Cattas art style, with matching kittens. Needs research: AI cost/servers, photo privacy, Steam's AI-content disclosure, and offline play. The part-based creator ships first; photo version later.
(Joke idea parked: a "drunk lads walking home from the pub" skin pack.)

## Home hub
A cosy house where trophies, unlocked skins and collectibles are displayed (it can double as the level-select: each room leads to its levels).

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
- [x] Step 2 – Trait bar UI + Litter Lord
- [x] Step 3 – Traits: Biscuit Baker, Fluffchute, Zoomies, Curtain Climber, Cable Chewer, Hairball Bridge, Scratcher – all 8 done!
- [x] Step 4 – Level data format + first 10 levels, timer, win/lose screens
- [x] Step 5 – Scrolling camera, level select menu, save data (zoom still to do)
- [ ] Demo build + Steam "Coming Soon" page (target: Christmas 2026)
- [ ] Art & audio integration
- [ ] Levels 11–100, NG+, cosmetics, home hub
- [ ] Phone / handheld port
- [ ] Level builder
