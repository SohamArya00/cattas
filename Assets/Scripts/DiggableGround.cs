using Unity.Collections;
using UnityEngine;

namespace Cattas
{
    // The level's ground, stored pixel by pixel so any part of it can be dug away.
    //
    // Game rules work on a 1-pixel grid (e.g. 640x360). The picture you SEE can be higher resolution
    // ("art scale" - e.g. 4 = 2560x1440 art), so painted levels look sharp while the rules stay simple.
    public class DiggableGround : MonoBehaviour
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int ArtScale { get; private set; } = 1;
        public Texture2D Texture => texture;

        bool[] solid;
        bool[] hard;   // stone: can't be dug, zoomed or zapped
        Texture2D texture;
        NativeArray<Color32> pixels; // points straight into the texture's memory
        int texW, texH;
        bool needsRedraw;
        bool hasArt;

        public void Init(int width, int height, int artScale = 1)
        {
            Width = width;
            Height = height;
            ArtScale = Mathf.Clamp(artScale, 1, 8);
            texW = width * ArtScale;
            texH = height * ArtScale;
            solid = new bool[width * height];
            hard = new bool[width * height];

            texture = new Texture2D(texW, texH, TextureFormat.RGBA32, false)
            {
                filterMode = ArtScale > 1 ? FilterMode.Bilinear : FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            pixels = texture.GetRawTextureData<Color32>();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);

            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(texture, new Rect(0, 0, texW, texH), Vector2.zero, ArtScale);
            sr.material = GameBootstrap.SpriteMaterial;
            sr.sortingOrder = 0;
        }

        void OnDestroy()
        {
            if (texture != null) Destroy(texture);
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

        // ---------- Painted art ----------

        // Use a painted picture as the ground: anything not see-through is solid.
        // The picture should be (Width x ArtScale) by (Height x ArtScale) pixels.
        public void ApplyArt(Texture2D art)
        {
            if (art == null) return;
            var src = art.GetPixels32(); // bottom row first, same as our y-up coordinates
            int aw = art.width, ah = art.height;
            for (int ty = 0; ty < texH; ty++)
            {
                int sy = Mathf.Min(ah - 1, ty * ah / texH);
                for (int tx = 0; tx < texW; tx++)
                {
                    int sx = Mathf.Min(aw - 1, tx * aw / texW);
                    pixels[ty * texW + tx] = src[sy * aw + sx];
                }
            }
            int half = ArtScale / 2;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    solid[y * Width + x] = pixels[(y * ArtScale + half) * texW + x * ArtScale + half].a > 127;
            hasArt = true;
            needsRedraw = true;
        }

        // Turn any solid ground inside this rectangle into stone (used with painted art + 'X' map cells).
        public void MakeHard(int x0, int y0, int x1, int y1)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(Height, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(Width, x1); x++)
                    if (solid[y * Width + x]) hard[y * Width + x] = true;
        }

        // ---------- Building ground from the text map ----------

        public void FillHardRect(int x0, int y0, int x1, int y1)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(Height, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(Width, x1); x++)
                {
                    solid[y * Width + x] = true;
                    hard[y * Width + x] = true;
                }
        }

        // A ramp filling one block. risingRight = "/" shape, otherwise "\" shape.
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

        // Colour everything in: grass on top, dirt underneath, grey stone.
        // (Skipped for painted levels - the art already has its colours.)
        public void FinishPainting()
        {
            if (!hasArt)
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        int i = y * Width + x;
                        if (!solid[i]) continue;
                        int n = Noise(x, y);
                        Color32 c;
                        if (hard[i])
                        {
                            byte g = (byte)(120 + n);
                            c = new Color32(g, g, (byte)(g + 8), 255);
                        }
                        else
                        {
                            bool nearTop = !IsSolid(x, y + 1) || !IsSolid(x, y + 2) || !IsSolid(x, y + 3);
                            c = nearTop
                                ? new Color32((byte)(60 + n), (byte)(150 + n), (byte)(50 + n / 2), 255)
                                : new Color32((byte)(115 + n), (byte)(75 + n / 2), (byte)(45 + n / 3), 255);
                        }
                        SetBlock(x, y, c);
                    }
                }
            }
            needsRedraw = true;
        }

        // ---------- Changing the ground during play ----------

        // The colour of the ground at a spot (used to colour dust particles).
        public Color ColourAt(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return new Color(0.6f, 0.5f, 0.4f);
            int half = ArtScale / 2;
            Color c = pixels[(y * ArtScale + half) * texW + x * ArtScale + half];
            if (c.a < 0.5f) return new Color(0.6f, 0.5f, 0.4f);
            c.a = 1f;
            return c;
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
                    SetBlock(x, y, new Color32((byte)Mathf.Min(255, colour.r + n), (byte)Mathf.Min(255, colour.g + n),
                                               (byte)Mathf.Min(255, colour.b + n), 255));
                    needsRedraw = true;
                }
            }
        }

        // Remove ground in a rectangle (x1/y1 not included). Stone is left alone.
        public void CarveRect(int x0, int y0, int x1, int y1)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(Height, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(Width, x1); x++)
                    ClearPixel(x, y);
        }

        // Dig a round hole. Stone is left alone.
        public void Carve(int cx, int cy, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                    if (dx * dx + dy * dy <= radius * radius)
                        ClearPixel(cx + dx, cy + dy);
        }

        void ClearPixel(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;
            int i = y * Width + x;
            if (!solid[i] || hard[i]) return;
            solid[i] = false;
            SetBlock(x, y, new Color32(0, 0, 0, 0));
            needsRedraw = true;
        }

        void SetBlock(int x, int y, Color32 c)
        {
            int k = ArtScale;
            for (int sy = 0; sy < k; sy++)
            {
                int row = (y * k + sy) * texW + x * k;
                for (int sx = 0; sx < k; sx++) pixels[row + sx] = c;
            }
        }

        // Is there any stone in this rectangle? (x1/y1 not included)
        public bool HasHardIn(int x0, int y0, int x1, int y1)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(Height, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(Width, x1); x++)
                    if (hard[y * Width + x]) return true;
            return false;
        }

        void LateUpdate()
        {
            if (!needsRedraw) return;
            texture.Apply(false);
            pixels = texture.GetRawTextureData<Color32>(); // refresh our view of the texture memory, just to be safe
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
