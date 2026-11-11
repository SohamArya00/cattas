using UnityEngine;

namespace Cattas
{
    public enum CatState { Falling, Walking, Splatted, Saved, Lost }

    // One cat. Every game step it walks, climbs small bumps, turns at walls,
    // and falls off edges. Falling too far = splat.
    public class Cat : MonoBehaviour
    {
        public const int MaxStepUp = 6;       // can walk up bumps this tall
        public const int MaxStepDown = 3;     // can walk down bumps this tall without "falling"
        public const int FallSpeed = 3;       // pixels per step while falling
        public const int SafeFallHeight = 60; // fall further than this and... splat

        public CatState State { get; private set; }

        int x, y;            // feet position in level pixels
        int dir = 1;         // 1 = right, -1 = left
        int fallDistance;

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

            if (State == CatState.Walking || State == CatState.Falling)
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

        void Walk()
        {
            int nextX = x + dir;

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

        void Fall()
        {
            for (int i = 0; i < FallSpeed; i++)
            {
                if (ground.IsSolid(x, y - 1)) { Land(); return; }
                y--;
                fallDistance++;
            }
        }

        void Land()
        {
            if (fallDistance > SafeFallHeight)
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
            sr.flipX = dir < 0;
        }
    }
}
