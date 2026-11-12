# CATTAS – Art Brief

This brief is for **placeholder AI art now** and for the **human artist later**. The sizes, layout rules and item list are the same for both.

> ⚠️ AI art is placeholder only. Anything AI-made that ships in the game or on the store page must be declared on Steam, so the release version will use human-made art.

## Style
- A cosy, dreamy, retro 1950s pastel home, seen **side-on and flat, like a 2D platform game or a dollhouse cut-away**
- Mint-green cabinets and appliances, cream and pink floral wallpaper, warm wood, checkered floor
- Soft warm light coming from the window, gentle shadows, a painterly or soft-3D look
- No people, no text, no logos, no real brands

## The golden rule: keep the layout
The game's kittens walk on the furniture, so **everything must stay where the layout guide puts it**: the worktop height, the fridge, the sink, the shelf, the cat tree and so on. Changing colours, textures and details is great. Moving or resizing things isn't.

## 1. Kitchen level (the most important one)
**Start from:** `docs/kitchen_layout_guide.png`

**How:** upload the guide to ChatGPT or Gemini and ask it to repaint it.

**Prompt:**
> Repaint this image as a beautiful cosy retro pastel kitchen illustration. Keep EVERY object in exactly the same position, size and shape, and keep the same flat side-on camera, like a 2D platform game. Soft warm window light, gentle shadows, painterly soft-3D style, mint green cabinets and fridge, floral wallpaper, checkered floor. Wide 16:9, highest resolution possible. No people, no text.

**Deliver:** one wide 16:9 image, as big as possible (ideally 2880×1620; 1920×1080 is OK). If the first try moves things around, ask: *"keep the exact layout of the original image"*.

## 2. Props and secret items
Each one goes on a **plain white background (or transparent), side view, the whole object visible, same style as the kitchen**:

| Item | Notes |
|---|---|
| Cookie jar | Glass jar full of cookies, pink lid |
| Cereal box | Fun made-up brand (no real logos) |
| Frying pan | Hanging by its handle, handle pointing up |
| Mouse trap | Wooden, with a little cheese wedge |
| Gas valve | Small red tap handle on a pipe |
| Golden yarn ball | Secret collectible: shiny gold, sparkly |
| Golden toy mouse | Secret collectible: shiny gold, sparkly |
| Golden fishbone | Secret collectible: shiny gold, sparkly |

## 3. Kittens (later – this one is hard for AI)
Animation needs **every frame to match exactly**, and AI isn't good at that yet. For now the game keeps its hand-coded HD kittens.

**For the human artist:**
- A sprite sheet of frames side by side, each frame **6:5** (e.g. 192×160 px), on a transparent background, facing right
- Frames: 4× walk cycle, 1× falling, 1× sitting/kneading
- Later: digging, climbing, zooming, building and the other trait poses

**Character:** based on Codi's seal-point Ragdoll-type cat. Cream body, dark brown face mask, ears, paws and tail, bright blue eyes, very fluffy, kitten proportions (big head and eyes).

## 4. Marketing (when we get to the Steam page)
- **Title logo** "CATTAS", playful and cosy
- **Capsule art**: kittens tumbling through the kitchen, with the hero cat on the fence in the rain as a story image
- **Steam sizes:** header capsule 920×430, small 462×174, main 1232×706, vertical 748×896, library hero 3840×1240

## Where the files go (for whoever wires them in)
- Art lives in `Assets/Resources/Art/` as **PNG files renamed to `.bytes`**. See the README section "Art pipeline".
- Claude can take a finished kitchen picture and cut out the walkable parts automatically, so artists only need to deliver the full picture.
