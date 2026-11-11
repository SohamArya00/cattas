using UnityEngine;

namespace Cattas
{
    // The level's ground, stored pixel by pixel so any part of it can be dug away.
    public class DiggableGround : MonoBehaviour
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        bool[] solid;
        bool[] hard;   // stone: can't be dug, zoomed or zapped
        Color32[] pixels;
        Texture2D texture;
        bool needsRedraw;

        public void Init(int width, int height)
        {
            Width = width;
            Height = height;
            solid = new bool[width * height];
            hard = new bool[width * height];
            pixels = new Color32[width * height];

            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.zero, 1f);
            sr.material = GameBootstrap.SpriteMaterial;
            sr.sortingOrder = 0;
        }

        // The left and right edges of the level act like walls; above and below are open air.
        public bool IsSolid(int x, int y)
        {
            if (x < 0 || x >= Width) return true;
            if (y < 0 || y >= Height) return false;
            return solid[y * Width + x];
        }

        public bool IsHard(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
            return hard[y * Width + x];
        }

        // Fill a rectangle with stone that can't be dug.
        public void FillHardRect(int x0, int y0, int x1, int y1)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(Height, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(Width, x1); x++)
                {
                    solid[y * Width + x] = true;
                    hard[y * Width + x] = true;
                }
        }

        // A ramp filling one 8x8 block. risingRight = "/" shape, otherwise "\" shape.
        public void FillRamp(int x0, int y0, int size, bool risingRight)
        {
            for (int i = 0; i < size; i++)
            {
                int h = risingRight ? i + 1 : size - i;
                FillRect(x0 + i, y0, x0 + i + 1, y0 + h);
            }
        }

        // Fill a rectangle with ground (x1/y1 not included).
        public void FillRect(int x0, int y0, int x1, int y1)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(Height, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(Width, x1); x++)
                    solid[y * Width + x] = true;
        }

        // A triangle-shaped hill sitting on baseY.
        public void FillHill(int centreX, int baseY, int halfWidth, int height)
        {
            for (int dx = -halfWidth; dx <= halfWidth; dx++)
            {
                int h = Mathf.RoundToInt(height * (1f - Mathf.Abs(dx) / (float)halfWidth));
                FillRect(centreX + dx, baseY, centreX + dx + 1, baseY + h);
            }
        }

        // Colour everything in: grass on top, dirt underneath.
        public void FinishPainting()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    if (!solid[i]) { pixels[i] = new Color32(0, 0, 0, 0); continue; }
                    int n = Noise(x, y);
                    if (hard[i])
                    {
                        byte g = (byte)(120 + n);
                        pixels[i] = new Color32(g, g, (byte)(g + 8), 255);
                        continue;
                    }
                    bool nearTop = !IsSolid(x, y + 1) || !IsSolid(x, y + 2) || !IsSolid(x, y + 3);
                    pixels[i] = nearTop
                        ? new Color32((byte)(60 + n), (byte)(150 + n), (byte)(50 + n / 2), 255)
                        : new Color32((byte)(115 + n), (byte)(75 + n / 2), (byte)(45 + n / 3), 255);
                }
            }
            needsRedraw = true;
        }

        // Dig a round hole.
        public void Carve(int cx, int cy, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radius * radius) continue;
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || x >= Width || y < 0 || y >= Height) continue;
                    int i = y * Width + x;
                    if (!solid[i] || hard[i]) continue;
                    solid[i] = false;
                    pixels[i] = new Color32(0, 0, 0, 0);
                    needsRedraw = true;
                }
            }
        }

        // Add new solid ground in a rectangle, in the given colour (used for hairballs).
        public void AddRect(int x0, int y0, int x1, int y1, Color32 colour)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(Height, y1); y++)
            {
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(Width, x1); x++)
                {
                    int i = y * Width + x;
                    if (hard[i]) continue;
                    int n = Noise(x, y) / 2;
                    solid[i] = true;
                    pixels[i] = new Color32((byte)Mathf.Min(255, colour.r + n), (byte)Mathf.Min(255, colour.g + n),
                                            (byte)Mathf.Min(255, colour.b + n), 255);
                    needsRedraw = true;
                }
            }
        }

        // Remove ground in a rectangle (x1/y1 not included).
        public void CarveRect(int x0, int y0, int x1, int y1)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(Height, y1); y++)
            {
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(Width, x1); x++)
                {
                    int i = y * Width + x;
                    if (!solid[i] || hard[i]) continue;
                    solid[i] = false;
                    pixels[i] = new Color32(0, 0, 0, 0);
                    needsRedraw = true;
                }
            }
        }

        bool AnyHard(int x0, int y0, int x1, int y1)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(Height, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(Width, x1); x++)
                    if (hard[y * Width + x]) return true;
            return false;
        }

        // Is there any stone in this rectangle? (x1/y1 not included)
        public bool HasHardIn(int x0, int y0, int x1, int y1) => AnyHard(x0, y0, x1, y1);

        void LateUpdate()
        {
            if (!needsRedraw) return;
            texture.SetPixels32(pixels);
            texture.Apply();
            needsRedraw = false;
        }

        static int Noise(int x, int y)
        {
            unchecked
            {
                int h = (x * 73856093) ^ (y * 19349663);
                return (h >> 4) & 31;
            }
        }
    }
}
