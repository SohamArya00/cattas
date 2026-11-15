# CATTAS – Game Design Doc (living document)

## Pitch
A modern, cat-themed take on the classic "guide the walkers to the exit" puzzle genre. 2.5D, beautiful high-res art, desktop first (Steam), then phone / handheld.

> Legal note: the genre and mechanics are free to use, but we never use the name "Lemmings", its art, levels, music or sounds. All content is original.

## Story
The Cattas are **your cat's kittens**, playfully trying to find their way back to you. Their adventure takes them through every room of the house and beyond. Finishing level 100 plays a closing cutscene: the game is complete (and NG+ opens).
- **Main character / mother cat:** Codi's cat, a seal-point Persian Chinchilla Ragdoll (the current placeholder art). She is the player's avatar.
- **Levels 1–5:** the kittens find their way to **mother cat for milk** (exit = mum and her milk bowl). Teaches the first **2 traits**, with a little story between levels.
- **Levels 6–10:** beginner tasks. **Grumpy Uncle Catta** (the first boss) arrives in the story, and the goal is the **litter basket**. Level 10 is his boss level.

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
- **Game speed:** slider from 50% (normal) to 100% (double). Some levels can start slower (players can only speed up). The full 1–100% range (slow-mo) is an NG+ unlock.
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

## Big pictures, small play areas (NG0 vs NG+)
Each level is one **big painted picture** that goes higher, lower and wider than you can use at first.
- In **NG0** only a **play area** (a box inside the picture, set with `area:` in the level file) is used. The camera and kittens stay inside it.
- The rest of the picture is shown **dimmed** as an "NG+ area". You can see it when you zoom out in the look-around mode, as a teaser.
- In **NG+**, super traits, golden traits, clothes and skins open up those areas: new routes, secrets and tasks in the same picture.
- Some objects in the picture work in NG0; most of the extras need NG+.

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
**Grumpy Uncle Catta**, a scruffy, funny-looking old tom, guards the way (grumpy and comically scruffy rather than starving, so it stays light-hearted).
**Boss fight idea:** send Cable Chewers at him: **10 ZAPs defeats him**, but each must go off at the right moment (e.g. when he's stomping past, or standing on the right spot). Get the kittens past him by going around him, or by finding the secret that chases him off:
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
| **Bring the Fallen Home** | Every lost kitten and every Biscuit Baker bounces home safely |
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
- **Breed builder:** build your mother cat from standard breeds, or mix them (e.g. Ragdoll + Chinchilla + Persian). The kittens are generated to match.
- **Your own cat:** take photos of your real cat (**front and side, sitting and standing**) and the game turns them into Cattas-style paper-doll pieces for the mother cat and her kittens. Needs research: AI cost/servers, photo privacy, and Steam's rules for **live-generated AI content** (guardrails required). The breed builder ships first; the photo version later.
- The paper-doll kitten (body, head, tail, legs) is what makes this work: a new cat is just new pieces with the same joints.
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

## Done this session
- Play area inside a big picture (`area:`), with the NG+ area dimmed and labelled in look-around mode
- Mother cat exit (levels 1–5) and litter tray exit (levels 6–10)
- Trait activation "ta-da": the kitten pops and looks up, sparkles, the trait name floats up

## Next session (agreed plan)
1. **Trait activation animations**: a short "ta-da" moment when a trait is given to a kitten (e.g. puff of fur and a sparkle, the kitten looks up, a little icon pops over its head), plus richer puppet poses per trait.
2. **Level 1 rebuilt as a real first level**: AI placeholder art (Codi generates it from a layout guide, Claude wires it in) and a gentle tutorial that teaches one or two traits (e.g. Litter Lord, then Biscuit Baker) with on-screen hints.
3. Ideas for how each early level teaches its trait (show, then let the player try, then twist).

## Roadmap
- [x] Step 0 – Unity installed, project created
- [x] Step 1 – Grey-box: walking, falling, splat, exit, destructible ground
- [x] Step 2 – Trait bar UI + Litter Lord
- [x] Step 3 – Traits: Biscuit Baker, Fluffchute, Zoomies, Curtain Climber, Cable Chewer, Hairball Bridge, Scratcher – all 8 done!
- [x] Step 4 – Level data format + first 10 levels, timer, win/lose screens
- [x] Step 5 – Scrolling camera, level select menu, save data (zoom still to do)
- [x] Kitchen demo level (placeholder art, juice, sounds) for pitching to the art & music team
- [x] Big kitchen demo: camera zoom/scroll, minimap, preview mode, hazards, switches, secrets, goals
- [ ] Demo build + Steam "Coming Soon" page (target: Christmas 2026)
- [ ] Art & audio integration
- [ ] Levels 11–100, NG+, cosmetics, home hub
- [ ] Phone / handheld port
- [ ] Level builder
