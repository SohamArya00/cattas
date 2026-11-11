using UnityEngine;

namespace Cattas
{
    public enum CatState { Falling, Walking, Digging, Blocking, Splatted, Saved, Lost }

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
                      || State == CatState.Digging || State == CatState.Blocking;

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
                if (h > MaxStepUp) { dir = -dir; return; } // it's a wall: turn around
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
            transform.localPosition = new Vector3(x + 0.5f, y, 0f);
            bool flip = dir < 0;
            if (State == CatState.Digging && wiggle) flip = !flip;
            if (State == CatState.Blocking)
            {
                // Knead knead knead: turn round every so often and squish on each push.
                if ((kneadTimer / 20) % 2 == 1) flip = !flip;
                float squish = (kneadTimer / 5) % 2 == 0 ? 1f : 0.85f;
                transform.localScale = new Vector3(1f, squish, 1f);
            }
            else if (IsFloating)
            {
                // All fluffed up like a dandelion, drifting down
                transform.localScale = new Vector3(1.4f, 1.4f, 1f);
            }
            else if (State != CatState.Splatted)
            {
                // Cats with a Fluffchute ready are permanently a bit fluffier, so you can spot them
                transform.localScale = hasFluffchute ? new Vector3(1.15f, 1.15f, 1f) : Vector3.one;
            }
            sr.flipX = flip;
        }
    }
}
