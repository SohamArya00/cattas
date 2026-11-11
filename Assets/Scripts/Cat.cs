using UnityEngine;

namespace Cattas
{
    public enum CatState { Falling, Walking, Digging, Blocking, Zooming, Climbing, Splatted, Saved, Lost }

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
        public const int ZoomTicksPerPixel = 2; // Zoomies move 1 pixel every 2 steps
        public const int ZoomTunnelHeight = 11; // tunnel is tall enough for a cat
        public const int ZoomReach = 5;       // how far in front the zooming cat smashes
        public const int ZoomLookAhead = 8;   // stops zooming when nothing is this close ahead

        public CatState State { get; private set; }
        public int X => x;
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

        GameBootstrap game;
        DiggableGround ground;
        SpriteRenderer sr;

        public void Init(GameBootstrap game, DiggableGround ground, int startX, int startY, Sprite sprite, Color tint)
        {
            this.game = game;
            this.ground = ground;
            x = startX;
            y = startY;
            State = CatState.Falling;

            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;
            sr.material = GameBootstrap.SpriteMaterial;
            sr.sortingOrder = 5;
            UpdateVisual();
        }

        public void Tick()
        {
            if (State == CatState.Walking) Walk();
            else if (State == CatState.Falling) Fall();
            else if (State == CatState.Digging) Dig();
            else if (State == CatState.Blocking) Knead();
            else if (State == CatState.Zooming) Zoom();
            else if (State == CatState.Climbing) Climb();

            if (IsActive)
            {
                if (game.IsAtExit(x, y))
                {
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

        bool IsActive => State == CatState.Walking || State == CatState.Falling
                      || State == CatState.Digging || State == CatState.Blocking
                      || State == CatState.Zooming || State == CatState.Climbing;

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
                game.OnCatLost(this);
                transform.localScale = new Vector3(1.4f, 0.4f, 1f); // squish!
                sr.color *= 0.6f;
                Destroy(gameObject, 1f);
            }
            else
            {
                State = CatState.Walking;
            }
        }

        void UpdateVisual()
        {
            if (State == CatState.Climbing)
            {
                // Turn sideways so the cat's paws are on the wall and its head points up
                transform.localRotation = Quaternion.Euler(0f, 0f, dir > 0 ? 90f : -90f);
                transform.localPosition = new Vector3(x + 0.5f + dir * 0.5f, y + CatHeight / 2f, 0f);
            }
            else
            {
                transform.localRotation = Quaternion.identity;
                transform.localPosition = new Vector3(x + 0.5f, y, 0f);
            }
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
            sr.flipX = flip;
        }
    }
}
