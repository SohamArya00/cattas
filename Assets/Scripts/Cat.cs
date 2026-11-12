using UnityEngine;

namespace Cattas
{
    public enum CatState { Falling, Walking, Digging, Blocking, Zooming, Climbing, Building, Scratching, Splatted, Saved, Lost, Zapped }

    // One cat. Every game step it walks, climbs small bumps, turns at walls,
    // and falls off edges. Falling too far = splat.
    public class Cat : MonoBehaviour
    {
        public const int MaxStepUp = 6;       // can walk up bumps this tall
        public const int MaxStepDown = 3;     // can walk down bumps this tall without "falling"
        public const int FallSpeed = 3;       // pixels per step while falling
        public const int SafeFallHeight = 60; // fall further than this and... splat
        public const int FloatSpeed = 1;      // Fluffchute: pixels per step while floating
        public const int FluffOpenHeight = 12; // Fluffchute puffs up after falling this far
        public const int DigTicksPerRow = 4;  // Litter Lord digs one pixel row every 4 steps
        public const int DigHalfWidth = 4;    // Litter Lord hole is 9 pixels wide
        public const int ClimbTicksPerPixel = 2; // Curtain Climber goes up 1 pixel every 2 steps
        public const int CatHeight = 10;
        public const int HairballsPerBridge = 12;
        public const int TicksPerHairball = GameBootstrap.TicksPerSecond; // one hairball a second
        public const int HairballWidth = 6, HairballHeight = 2, HairballStepForward = 4;
        public const int FuseSeconds = 5;
        public const int ZapRadius = 12;
        public const int ScratchTicksPerStep = 4; // Scratcher moves 1 across + 1 down every 4 steps
        public const int ScratchReach = 4;        // how far in front it claws away
        public const int ZoomTicksPerPixel = 2; // Zoomies move 1 pixel every 2 steps
        public const int ZoomTunnelHeight = 11; // tunnel is tall enough for a cat
        public const int ZoomReach = 5;       // how far in front the zooming cat smashes
        public const int ZoomLookAhead = 12;  // stops zooming when nothing is this close ahead

        public CatState State { get; private set; }
        public int X => x;
        public int Dir => dir;
        public int Y => y;

        int x, y;            // feet position in level pixels
        int dir = 1;         // 1 = right, -1 = left
        int fallDistance;
        int digTimer;
        bool wiggle;
        int kneadTimer;
        bool hasFluffchute; // permanent once given
        int zoomTimer;
        bool hasClimber;    // permanent once given
        int climbTimer;
        int hairballsLeft, hackTimer, bridgeStepsLeft;
        int scratchTimer;
        int fuseTicks = -1;  // Cable Chewer countdown (-1 = not chewing)
        Color baseTint;      // the cat's normal colour

        public int FuseSecondsLeft => fuseTicks < 0 ? 0 : Mathf.CeilToInt(fuseTicks / (float)GameBootstrap.TicksPerSecond);

        static readonly Color32 HairballColour = new Color32(140, 120, 100, 255);

        GameBootstrap game;
        DiggableGround ground;
        SpriteRenderer sr;     // single-picture kitten (used when there's no paper-doll rig)
        CatPuppet puppet;      // paper-doll kitten (body, head, tail and legs animated separately)
        public CatPuppet Puppet => puppet;
        public Sprite StillSprite => sr != null ? sr.sprite : null;
        public Color Tint => baseTint;

        // Animation: frames 0-3 walk cycle, 4 falling, 5 sitting. (null = single still picture)
        Sprite[] frames;
        int animTick;
        bool puffed;

        // Smooth movement: the rules move the cat once per game step; the picture glides between steps.
        Vector3 prevPos, curPos;

        public void Init(GameBootstrap game, DiggableGround ground, int startX, int startY, Sprite sprite, Color tint,
                         Sprite[] animFrames = null, int startDir = 1)
        {
            dir = startDir < 0 ? -1 : 1;
            frames = animFrames != null && animFrames.Length >= 6 ? animFrames : null;
            this.game = game;
            this.ground = ground;
            x = startX;
            y = startY;
            State = CatState.Falling;

            baseTint = tint;
            if (game.KittenRig != null)
            {
                puppet = CatPuppet.Create(transform, game.KittenRig, tint, 5);
            }
            else
            {
                sr = gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = tint;
                sr.material = GameBootstrap.SpriteMaterial;
                sr.sortingOrder = 5;
            }
            UpdateVisual();
            prevPos = curPos;
            transform.localPosition = curPos;
        }

        void LateUpdate()
        {
            if (State == CatState.Splatted) return;
            transform.localPosition = Vector3.Lerp(prevPos, curPos, GameBootstrap.TickAlpha);
        }

        public void Tick()
        {
            // Cable Chewer countdown runs whatever else the cat is doing
            if (IsActive && fuseTicks > 0 && --fuseTicks == 0)
            {
                Zap();
                return;
            }

            if (State == CatState.Walking) Walk();
            else if (State == CatState.Falling) Fall();
            else if (State == CatState.Digging) Dig();
            else if (State == CatState.Blocking) Knead();
            else if (State == CatState.Zooming) Zoom();
            else if (State == CatState.Climbing) Climb();
            else if (State == CatState.Building) BuildBridge();
            else if (State == CatState.Scratching) Scratch();

            if (IsActive)
            {
                if (game.IsAtExit(x, y))
                {
                    game.Fx(FxKind.Home, x, y);
                    State = CatState.Saved;
                    game.OnCatSaved(this);
                    Destroy(gameObject);
                    return;
                }
                if (y < -20) // fell out of the bottom of the level
                {
                    State = CatState.Lost;
                    game.OnCatLost(this);
                    Destroy(gameObject);
                    return;
                }
            }
            UpdateVisual();
        }

        // Still in play (not home, lost or splatted)
        public bool IsActive => State == CatState.Walking || State == CatState.Falling
                      || State == CatState.Digging || State == CatState.Blocking
                      || State == CatState.Zooming || State == CatState.Climbing
                      || State == CatState.Building || State == CatState.Scratching;

        // Called when the player clicks this cat with a trait selected.
        // Returns true if the cat accepted it (so the trait gets used up).
        public bool TryGiveTrait(CatTrait trait)
        {
            switch (trait)
            {
                case CatTrait.LitterLord:
                    if (State != CatState.Walking) return false; // must be standing on ground
                    State = CatState.Digging;
                    digTimer = 0;
                    return true;
                case CatTrait.Fluffchute:
                    // Can be given while walking, falling or digging - and it lasts forever.
                    if (hasFluffchute) return false;
                    if (State != CatState.Walking && State != CatState.Falling && State != CatState.Digging) return false;
                    hasFluffchute = true;
                    return true;
                case CatTrait.Scratcher:
                    if (State != CatState.Walking) return false;
                    State = CatState.Scratching;
                    scratchTimer = 0;
                    return true;
                case CatTrait.CableChewer:
                    // Any busy cat can start chewing - even a Biscuit Baker (that's how you free them!)
                    if (fuseTicks >= 0) return false;
                    fuseTicks = FuseSeconds * GameBootstrap.TicksPerSecond;
                    return true;
                case CatTrait.HairballBridge:
                    if (State != CatState.Walking) return false;
                    State = CatState.Building;
                    hairballsLeft = HairballsPerBridge;
                    hackTimer = 0;
                    bridgeStepsLeft = 0;
                    return true;
                case CatTrait.CurtainClimber:
                    // Permanent, like Fluffchute. Kicks in next time the cat walks into a wall.
                    if (hasClimber) return false;
                    if (State == CatState.Blocking) return false;
                    hasClimber = true;
                    return true;
                case CatTrait.Zoomies:
                    if (State != CatState.Walking) return false;
                    State = CatState.Zooming;
                    zoomTimer = 0;
                    return true;
                case CatTrait.BiscuitBaker:
                    if (State != CatState.Walking) return false;
                    State = CatState.Blocking;
                    kneadTimer = 0;
                    return true;
                default:
                    return false; // not built yet
            }
        }

        void Dig()
        {
            if (++digTimer < DigTicksPerRow) return;
            digTimer = 0;

            // Is there any ground left under us to scratch?
            bool groundBelow = false;
            for (int dx = -DigHalfWidth; dx <= DigHalfWidth; dx++)
                if (ground.IsSolid(x + dx, y - 1)) { groundBelow = true; break; }

            if (!groundBelow)
            {
                // Broke through! Back to normal: fall, then walk on.
                State = CatState.Falling;
                fallDistance = 0;
                return;
            }

            if (ground.HasHardIn(x - DigHalfWidth, y - 1, x + DigHalfWidth + 1, y))
            {
                State = CatState.Walking; // clunk - stone. Give up digging.
                return;
            }

            game.Fx(FxKind.Dig, x, y - 1);
            ground.CarveRect(x - DigHalfWidth, y - 1, x + DigHalfWidth + 1, y);
            y--;
            wiggle = !wiggle; // flip side to side so it looks like scratching
        }

        // Biscuit Baker: stay put and knead. If the ground under us vanishes, we fall
        // (and stop baking).
        void Knead()
        {
            kneadTimer++;
            if (!ground.IsSolid(x, y - 1))
            {
                State = CatState.Falling;
                fallDistance = 0;
            }
        }

        // Zoomies: charge forward, smashing a cat-sized tunnel through anything in the way.
        // Stops (and goes back to walking) when there's nothing left to smash.
        void Zoom()
        {
            if (++zoomTimer < ZoomTicksPerPixel) return;
            zoomTimer = 0;

            // Reached the edge of the level, or a Biscuit Baker? Calm down and turn round.
            int nextX = x + dir;
            if (nextX < 0 || nextX >= ground.Width || game.IsBlockedByBaker(this, nextX, y))
            {
                dir = -dir;
                State = CatState.Walking;
                return;
            }

            // Anything ahead worth smashing?
            if (!SolidAhead(ZoomLookAhead))
            {
                State = CatState.Walking;
                return;
            }

            // SMASH the bit right in front of us, then move forward.
            int a = x + dir, b = x + dir * ZoomReach;
            // Stone or metal in the way of our legs: bonk, zoomies over. (Stone overhead doesn't stop us.)
            if (ground.HasHardIn(Mathf.Min(a, b), y, Mathf.Max(a, b) + 1, y + 4))
            {
                State = CatState.Walking;
                return;
            }
            game.Fx(FxKind.Smash, x + dir * 3, y + 4);
            ground.CarveRect(Mathf.Min(a, b), y, Mathf.Max(a, b) + 1, y + ZoomTunnelHeight);
            x = nextX;
            wiggle = !wiggle;

            // Keep our feet on the ground (small dips are fine, big drops mean falling)
            for (int d = 0; d <= MaxStepDown; d++)
            {
                if (ground.IsSolid(x, y - 1 - d)) { y -= d; return; }
            }
            State = CatState.Falling;
            fallDistance = 0;
        }

        // Curtain Climber: go straight up the wall. At the top, hop over onto it.
        // If something bonks our head on the way up, let go and fall back.
        void Climb()
        {
            if (++climbTimer < ClimbTicksPerPixel) return;
            climbTimer = 0;

            int wallX = x + dir;
            if (ground.IsSolid(x, y + CatHeight) || y + CatHeight >= ground.Height)
            {
                // Bonk! Ceiling. Let go and fall.
                dir = -dir;
                State = CatState.Falling;
                fallDistance = 0;
                return;
            }

            y++;
            if (!ground.IsSolid(wallX, y))
            {
                // Made it to the top - step over onto it.
                x = wallX;
                State = CatState.Walking;
            }
        }

        // Hit by a hazard (water, fire): the kitten goes home grumpy to dry off / cool down - not saved.
        public void Hazard(FxKind kind)
        {
            if (!IsActive) return;
            game.Fx(kind, x, y);
            game.SpawnSadCat(this, kind);
            State = CatState.Lost;
            game.OnCatLost(this);
            Destroy(gameObject);
        }

        // Scratcher: claw a sloping tunnel diagonally down and forwards (one across, one down).
        void Scratch()
        {
            if (++scratchTimer < ScratchTicksPerStep) return;
            scratchTimer = 0;

            int nextX = x + dir;
            if (nextX < 0 || nextX >= ground.Width)
            {
                dir = -dir;
                State = CatState.Walking;
                return;
            }

            // Broke out into open air? Then we're done.
            if (!SolidInFrontOrBelow())
            {
                State = CatState.Walking;
                return;
            }

            // Claw away the ground in front of us and just below our feet line
            int a = x + dir, b = x + dir * ScratchReach;
            // Stone or metal right where our paws are? Then we can't get through.
            // (Stone higher up is fine - the claws just go underneath it.)
            if (ground.HasHardIn(Mathf.Min(a, b), y - 1, Mathf.Max(a, b) + 1, y + 3))
            {
                State = CatState.Walking;
                return;
            }
            game.Fx(FxKind.Dig, x + dir * 2, y);
            ground.CarveRect(Mathf.Min(a, b), y - 1, Mathf.Max(a, b) + 1, y + CatHeight);
            x = nextX;
            y--;
            wiggle = !wiggle;

            if (!ground.IsSolid(x, y - 1))
            {
                State = CatState.Falling;
                fallDistance = 0;
            }
        }

        bool SolidInFrontOrBelow()
        {
            for (int i = 1; i <= ScratchReach + 2; i++)
            {
                int cx = x + dir * i;
                if (cx < 0 || cx >= ground.Width) return false;
                for (int h = -1; h < CatHeight; h++)
                    if (ground.IsSolid(cx, y + h)) return true;
            }
            return false;
        }

        // Cable Chewer: ZAP! Blast a round hole, and the frizzy cat shoots off home to recover.
        void Zap()
        {
            game.Fx(FxKind.Zap, x, y + 1);
            ground.Carve(x, y + 1, ZapRadius);
            game.SpawnZap(this);
            State = CatState.Zapped;
            game.OnCatLost(this);
            Destroy(gameObject);
        }

        // Hairball Bridge: hack up a hairball (one a second), step up onto it, repeat 12 times.
        void BuildBridge()
        {
            if (!ground.IsSolid(x, y - 1))
            {
                State = CatState.Falling;
                fallDistance = 0;
                return;
            }

            // Walking up onto the hairball we just made, one pixel per step
            if (bridgeStepsLeft > 0)
            {
                int nextX = x + dir;
                if (nextX < 0 || nextX >= ground.Width || ground.IsSolid(nextX, y + CatHeight) || ground.IsSolid(nextX, y + HairballHeight + 1))
                {
                    // Bumped into something - stop building and turn round
                    dir = -dir;
                    State = CatState.Walking;
                    return;
                }
                if (ground.IsSolid(nextX, y))
                {
                    int h = 1;
                    while (h <= HairballHeight && ground.IsSolid(nextX, y + h)) h++;
                    y += h;
                }
                x = nextX;
                bridgeStepsLeft--;
                if (bridgeStepsLeft == 0 && hairballsLeft == 0) State = CatState.Walking;
                return;
            }

            // Hrrk... hrrk... HACK!
            if (++hackTimer < TicksPerHairball) return;
            hackTimer = 0;

            int a = x + dir, b = x + dir * HairballWidth;
            ground.AddRect(Mathf.Min(a, b), y, Mathf.Max(a, b) + 1, y + HairballHeight, HairballColour);
            game.Fx(FxKind.Hairball, x + dir * 4, y + 1);
            hairballsLeft--;
            bridgeStepsLeft = HairballStepForward;
        }

        bool SolidAhead(int distance)
        {
            for (int i = 1; i <= distance; i++)
            {
                int cx = x + dir * i;
                if (cx < 0 || cx >= ground.Width) return false;
                for (int h = 0; h < ZoomTunnelHeight; h++)
                    if (ground.IsSolid(cx, y + h)) return true;
            }
            return false;
        }

        void Walk()
        {
            int nextX = x + dir;

            // A Biscuit Baker in front of us? Turn around.
            if (game.IsBlockedByBaker(this, nextX, y)) { dir = -dir; return; }

            if (ground.IsSolid(nextX, y))
            {
                // Something in the way - is it a small step we can climb?
                int h = 1;
                while (h <= MaxStepUp && ground.IsSolid(nextX, y + h)) h++;
                if (h > MaxStepUp)
                {
                    // It's a wall! Climbers climb it (but not the edge of the level); everyone else turns round.
                    if (hasClimber && nextX >= 0 && nextX < ground.Width)
                    {
                        State = CatState.Climbing;
                        climbTimer = 0;
                    }
                    else
                    {
                        dir = -dir;
                    }
                    return;
                }
                x = nextX;
                y += h;
                return;
            }

            x = nextX;
            // Is there ground just below us?
            for (int d = 0; d <= MaxStepDown; d++)
            {
                if (ground.IsSolid(x, y - 1 - d)) { y -= d; return; }
            }
            State = CatState.Falling;
            fallDistance = 0;
        }

        bool IsFloating => State == CatState.Falling && hasFluffchute && fallDistance >= FluffOpenHeight;

        void Fall()
        {
            if (IsFloating && !puffed)
            {
                puffed = true;
                game.Fx(FxKind.Puff, x, y + 5);
            }
            int speed = IsFloating ? FloatSpeed : FallSpeed;
            for (int i = 0; i < speed; i++)
            {
                if (ground.IsSolid(x, y - 1)) { Land(); return; }
                y--;
                fallDistance++;
            }
        }

        void Land()
        {
            if (fallDistance > SafeFallHeight && !hasFluffchute)
            {
                State = CatState.Splatted;
                game.Fx(FxKind.Splat, x, y);
                game.OnCatLost(this);
                transform.localScale = new Vector3(1.4f, 0.4f, 1f); // squish!
                SetColour(baseTint * 0.6f);
                Destroy(gameObject, 1f);
            }
            else
            {
                State = CatState.Walking;
                puffed = false;
            }
        }

        void UpdateVisual()
        {
            animTick++;
            prevPos = curPos;
            if (State == CatState.Climbing)
            {
                // Turn sideways so the cat's paws are on the wall and its head points up
                transform.localRotation = Quaternion.Euler(0f, 0f, dir > 0 ? 90f : -90f);
                curPos = new Vector3(x + 0.5f + dir * 0.5f, y + CatHeight / 2f, 0f);
            }
            else
            {
                transform.localRotation = Quaternion.identity;
                curPos = new Vector3(x + 0.5f, y, 0f);
            }
            // Big jumps (e.g. turning sideways to climb) shouldn't glide
            if ((curPos - prevPos).sqrMagnitude > 64f) prevPos = curPos;

            if (frames != null && sr != null) sr.sprite = frames[PickFrame()];
            bool flip = dir < 0;
            if (State == CatState.Digging && wiggle) flip = !flip;
            if (State == CatState.Blocking)
            {
                // Knead knead knead: turn round every so often and squish on each push.
                if ((kneadTimer / 20) % 2 == 1) flip = !flip;
                float squish = (kneadTimer / 5) % 2 == 0 ? 1f : 0.85f;
                transform.localScale = new Vector3(1f, squish, 1f);
            }
            else if (State == CatState.Zooming)
            {
                // Stretched out at full speed
                transform.localScale = wiggle ? new Vector3(1.25f, 0.9f, 1f) : new Vector3(1.1f, 0.95f, 1f);
            }
            else if (State == CatState.Building && bridgeStepsLeft == 0 && hackTimer > TicksPerHairball - 8)
            {
                // Hrrk... hrrk... (squish as it coughs one up)
                transform.localScale = (hackTimer / 2) % 2 == 0 ? new Vector3(1.15f, 0.85f, 1f) : new Vector3(0.95f, 1.05f, 1f);
            }
            else if (State == CatState.Scratching)
            {
                // Clawing away, hunched over
                transform.localScale = wiggle ? new Vector3(1.1f, 0.9f, 1f) : Vector3.one;
            }
            else if (IsFloating)
            {
                // All fluffed up like a dandelion, drifting down
                transform.localScale = new Vector3(1.4f, 1.4f, 1f);
            }
            else if (State != CatState.Splatted)
            {
                // Cats with a Fluffchute or Curtain Climber are permanently a bit fluffier, so you can spot them
                transform.localScale = (hasFluffchute || hasClimber) ? new Vector3(1.15f, 1.15f, 1f) : Vector3.one;
            }
            if (puppet != null)
            {
                // the puppet does its own leg-swinging, so it only needs to know what we're up to
                puppet.flip = dir < 0;
                puppet.state = State;
                puppet.floating = IsFloating;
                puppet.hacking = State == CatState.Building && bridgeStepsLeft == 0 && hackTimer > TicksPerHairball - 8;
                if (State == CatState.Blocking && (kneadTimer / 20) % 2 == 1) puppet.flip = !puppet.flip;
                if (State == CatState.Blocking || State == CatState.Building || State == CatState.Scratching || State == CatState.Digging)
                    transform.localScale = Vector3.one; // the puppet animates these itself
            }
            else sr.flipX = flip;

            // Sparks! Flicker yellow while chewing a cable
            if (fuseTicks > 0)
                SetColour((fuseTicks / 3) % 2 == 0 ? Color.Lerp(baseTint, Color.yellow, 0.7f) : baseTint);
        }

        void SetColour(Color c)
        {
            if (puppet != null) puppet.SetColor(c);
            else if (sr != null) sr.color = c;
        }

        int PickFrame()
        {
            switch (State)
            {
                case CatState.Falling: return 4;
                case CatState.Blocking: return 5;
                case CatState.Digging: return 5;
                case CatState.Zooming: return (animTick / 2) % 4;
                case CatState.Climbing: return (animTick / 5) % 4;
                case CatState.Scratching: return (animTick / 6) % 4;
                case CatState.Building: return bridgeStepsLeft > 0 ? (animTick / 3) % 4 : 5;
                default: return (animTick / 4) % 4;
            }
        }
    }
}
