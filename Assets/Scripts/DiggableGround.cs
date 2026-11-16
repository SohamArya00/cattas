using Unity.Collections;
using UnityEngine;

namespace Cattas
{
    // The level's ground, stored pixel by pixel so any part of it can be dug away.
    //
    // Game rules work on a 1-pixel grid (e.g. 640x360). The picture you SEE can be higher resolution
    // ("art scale" - e.g. 4 = 2560x1440 art), so painted levels look sharp while the rules stay simple.
    //
    // DEPTH PLANES (for 2.5D pictures): every solid pixel belongs to the BACK plane (1), the FRONT plane (2) or
    // both (3). A kitten only collides with the planes it is in, so a kitten walking on a step "behind" a stool
    // can pass it, while a kitten on the floor "in front of" the steps passes them. Ordinary levels use 3 everywhere.
    //
    // Painted levels can be "collision only": the picture itself is never touched. The ground texture is then a
    // see-through layer that only shows effects on top of the picture (holes you dug, hairballs).
    public class DiggableGround : MonoBehaviour
    {
        // Depth layers (lanes). A pixel belongs to one or more; a kitten is blocked only by pixels in its own lane.
        // Back = things against the wall, Front = floor-level things, Mid = things in the middle of the room.
        public const int Back = 1, Front = 2, Mid = 4, Both = 7;

        // When on, a kitten can STAND on a surface from any lane (landing/stepping onto it moves it to that lane)
        public bool AnySupport;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int ArtScale { get; private set; } = 1;
        public Texture2D Texture => texture;

        // Which plane(s) the kitten currently being worked on is in. Set by Cat.Tick.
        public int ActivePlane = Both;

        // The play area: kittens can only use this part of the picture (the rest is for NG+ later).
        // Its left and right sides act like walls, and below its bottom is a fall out of the level.
        public int MinX { get; private set; }
        public int MaxX { get; private set; }
        public int MinY { get; private set; }
        public int MaxY { get; private set; }

        public void SetPlayArea(RectInt area)
        {
            MinX = Mathf.Clamp(area.xMin, 0, Width);
            MaxX = Mathf.Clamp(area.xMax, MinX + 1, Width);
            MinY = Mathf.Clamp(area.yMin, 0, Height);
            MaxY = Mathf.Clamp(area.yMax, MinY + 1, Height);
        }

        byte[] planes;   // 0 = air, otherwise Back / Front / Both
        bool[] hard;     // stone: can't be dug, zoomed or zapped
        Texture2D texture;
        NativeArray<Color32> pixels; // points straight into the texture's memory
        int texW, texH;
        bool needsRedraw;
        bool hasArt;
        bool overlayOnly;            // collision-only painted level: draw dug holes as a dark overlay
        Texture2D colourSource;      // where particle colours come from (the level picture)
        Color32[] colourPixels;
        // The standard "something was dug here" look (used for every dig: Litter Lord, Zoomies, Scratcher, ZAP...):
        // a soft earthy hole that carries on down to the next surface, with loose crumbs of dirt at its lip.
        static readonly Color32 HoleTop = new Color32(36, 21, 17, 246);       // darkest, under the lip
        static readonly Color32 HoleBottom = new Color32(92, 58, 42, 240);    // lighter where the light reaches
        static readonly Color32 Crumb1 = new Color32(112, 72, 52, 235);
        static readonly Color32 Crumb2 = new Color32(146, 98, 70, 225);
        const int ShaftMax = 44;     // how far a hole is drawn carrying on down through empty space
        bool[] dug;                  // pixels dug out of the picture
        bool[] deco;                 // pixels drawn only as decoration (the shaft below a hole, crumbs at its lip)
        bool[] built;                // pixels that are hairball bridge
        byte[] builtLane;            // the lane of the kitten that built each hairball

        public void Init(int width, int height, int artScale = 1)
        {
            Width = width;
            Height = height;
            MinX = 0; MaxX = width; MinY = 0; MaxY = height;
            ArtScale = Mathf.Clamp(artScale, 1, 8);
            texW = width * ArtScale;
            texH = height * ArtScale;
            planes = new byte[width * height];
            hard = new bool[width * height];
            dug = new bool[width * height];
            deco = new bool[width * height];
            built = new bool[width * height];
            builtLane = new byte[width * height];

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

        // The left and right edges of the play area act like walls; above and below are open air.
        public bool IsSolid(int x, int y)
        {
            if (x < MinX || x >= MaxX) return true;
            if (y < MinY || y >= MaxY) return false;
            return (planes[y * Width + x] & ActivePlane) != 0;
        }

        bool Solid(int i) => planes[i] != 0;

        // Something to stand on at this pixel? With AnySupport on, solid pixels of every lane count.
        public bool IsSupport(int x, int y)
        {
            if (!AnySupport) return IsSolid(x, y);
            if (x < MinX || x >= MaxX) return true;
            if (y < MinY || y >= MaxY) return false;
            return planes[y * Width + x] != 0;
        }

        // Which lanes the solid pixel at (x, y) belongs to (0 = nothing there)
        public int MaskAt(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return 0;
            return planes[y * Width + x];
        }

        public bool IsHard(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
            int i = y * Width + x;
            return hard[i] && (planes[i] & ActivePlane) != 0;
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
                    planes[y * Width + x] = pixels[(y * ArtScale + half) * texW + x * ArtScale + half].a > 127 ? (byte)Both : (byte)0;
            hasArt = true;
            needsRedraw = true;
        }

        // Collision-only painted level: a data picture says what is solid, and the level picture is never touched.
        //   opaque = solid;  red channel >127 = back plane;  green channel >127 = front plane;  neither = both planes.
        public void ApplyCollision(Texture2D collision)
        {
            if (collision == null) return;
            var src = collision.GetPixels32();
            int aw = collision.width, ah = collision.height;
            for (int y = 0; y < Height; y++)
            {
                int sy = Mathf.Min(ah - 1, (int)((y + 0.5f) * ah / Height));
                for (int x = 0; x < Width; x++)
                {
                    int sx = Mathf.Min(aw - 1, (int)((x + 0.5f) * aw / Width));
                    var c = src[sy * aw + sx];
                    if (c.a <= 127) continue;
                    int m = (c.r > 127 ? Back : 0) | (c.g > 127 ? Front : 0) | (c.b > 127 ? Mid : 0);   // red = back, green = front, blue = mid
                    planes[y * Width + x] = (byte)(m == 0 ? Both : m);
                }
            }
            hasArt = true;
            overlayOnly = true;
        }

        // Particle colours (dust, debris) come from the level picture
        public void SetColourSource(Texture2D picture)
        {
            colourSource = picture;
            colourPixels = picture != null ? picture.GetPixels32() : null;
        }

        // Use a picture to mark stone: anything solid where the picture is opaque can't be dug.
        public void MakeHardFromArt(Texture2D art)
        {
            if (art == null) return;
            var src = art.GetPixels32();
            int aw = art.width, ah = art.height;
            for (int y = 0; y < Height; y++)
            {
                int sy = Mathf.Min(ah - 1, (int)((y + 0.5f) * ah / Height));
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    if (!Solid(i)) continue;
                    int sx = Mathf.Min(aw - 1, (int)((x + 0.5f) * aw / Width));
                    if (src[sy * aw + sx].a > 127) hard[i] = true;
                }
            }
        }

        // Turn any solid ground inside this rectangle into stone (used with painted art + 'X' map cells).
        public void MakeHard(int x0, int y0, int x1, int y1)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(Height, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(Width, x1); x++)
                    if (Solid(y * Width + x)) hard[y * Width + x] = true;
        }

        // ---------- Building ground from the text map ----------

        public void FillHardRect(int x0, int y0, int x1, int y1)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(Height, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(Width, x1); x++)
                {
                    planes[y * Width + x] = Both;
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
                    planes[y * Width + x] = Both;
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
                        if (!Solid(i)) continue;
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
            var fallback = new Color(0.6f, 0.5f, 0.4f);
            if (x < 0 || x >= Width || y < 0 || y >= Height) return fallback;
            if (colourPixels != null)
            {
                int sx = Mathf.Min(colourSource.width - 1, (int)((x + 0.5f) * colourSource.width / Width));
                int sy = Mathf.Min(colourSource.height - 1, (int)((y + 0.5f) * colourSource.height / Height));
                Color pc = colourPixels[sy * colourSource.width + sx];
                pc.a = 1f;
                return pc * 0.85f;
            }
            int half = ArtScale / 2;
            Color c = pixels[(y * ArtScale + half) * texW + x * ArtScale + half];
            if (c.a < 0.5f) return fallback;
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
                    if ((planes[i] & ActivePlane) != 0) continue;   // already solid for this kitten: no hairballs inside solid things
                    int n = Noise(x, y) / 2;
                    planes[i] = Both;                 // a bridge is solid for every lane (everyone can climb the stairs)
                    dug[i] = false;
                    built[i] = true;
                    builtLane[i] = (byte)ActivePlane;
                    SetBlock(x, y, new Color32((byte)Mathf.Min(255, colour.r + n), (byte)Mathf.Min(255, colour.g + n),
                                               (byte)Mathf.Min(255, colour.b + n), 255));
                    needsRedraw = true;
                }
            }
            RefreshHoles(x0 - 1, y0 - 1, x1 + 1, y1 + 1);
        }

        // How many solid pixels are inside a rectangle (any plane) - used to see if a vase or faucet is still there
        public int CountSolid(RectInt r)
        {
            int n = 0;
            for (int y = Mathf.Max(0, r.yMin); y < Mathf.Min(Height, r.yMax); y++)
                for (int x = Mathf.Max(0, r.xMin); x < Mathf.Min(Width, r.xMax); x++)
                    if (planes[y * Width + x] != 0) n++;
            return n;
        }

        // Is this pixel part of a hairball bridge? (kittens standing on one are a little better at stepping up)
        public bool IsBuilt(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
            int i = y * Width + x;
            return built[i] && (planes[i] & ActivePlane) != 0;
        }

        // A bridge pixel built by a kitten in my lane (kittens running a rim step onto stairs that start on their path)
        public bool IsBuiltInMyLane(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
            int i = y * Width + x;
            return built[i] && (builtLane[i] & ActivePlane) != 0;
        }

        // Remove ground in a rectangle (x1/y1 not included). Stone is left alone.
        public void CarveRect(int x0, int y0, int x1, int y1)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(Height, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(Width, x1); x++)
                    ClearPixel(x, y);
            RefreshHoles(x0 - 1, y0 - 1, x1 + 1, y1 + 1);
        }

        // Dig a round hole. Stone is left alone.
        public void Carve(int cx, int cy, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                    if (dx * dx + dy * dy <= radius * radius)
                        ClearPixel(cx + dx, cy + dy);
            RefreshHoles(cx - radius - 1, cy - radius - 1, cx + radius + 2, cy + radius + 2);
        }

        void ClearPixel(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;
            int i = y * Width + x;
            if ((planes[i] & ActivePlane) == 0 || hard[i]) return;
            planes[i] = built[i] ? (byte)0 : (byte)(planes[i] & ~ActivePlane);   // a bridge is smashed for every lane
            built[i] = false;
            if (planes[i] != 0) return;           // still solid in the other plane: the picture stays as it is
            if (overlayOnly) dug[i] = true;       // drawn by RefreshHoles
            else SetBlock(x, y, new Color32(0, 0, 0, 0));
            needsRedraw = true;
        }

        // Draw the standard hole look: a soft earthy hole (no outline), darker under the lip and lighter below,
        // which carries on down through empty space to the next surface, with crumbs of dirt at the lip.
        void RefreshHoles(int x0, int y0, int x1, int y1)
        {
            if (!overlayOnly) return;
            int rx0 = Mathf.Max(0, x0 - 3), rx1 = Mathf.Min(Width, x1 + 3);
            int ry0 = Mathf.Max(0, y0 - ShaftMax - 1), ry1 = Mathf.Min(Height, y1 + 4);

            // 1. forget the old decoration in this area
            for (int y = ry0; y < ry1; y++)
                for (int x = rx0; x < rx1; x++)
                {
                    int i = y * Width + x;
                    if (!deco[i]) continue;
                    deco[i] = false;
                    if (planes[i] == 0 && !dug[i]) SetBlock(x, y, new Color32(0, 0, 0, 0));
                }

            // 2. the holes themselves
            for (int y = Mathf.Max(0, y0 - 1); y < Mathf.Min(Height, y1 + 1); y++)
            {
                for (int x = rx0; x < rx1; x++)
                {
                    int i = y * Width + x;
                    if (!dug[i]) continue;

                    int d = 4;                                               // distance to the nearest hole edge (1..4)
                    for (int k = 1; k <= 3 && d == 4; k++)
                        if (!IsDug(x - k, y) || !IsDug(x + k, y) || !IsDug(x, y - k) || !IsDug(x, y + k)) d = k;
                    int up = 0, down = 0;
                    while (up < 24 && IsDug(x, y + up + 1)) up++;
                    while (down < 24 && IsDug(x, y - down - 1)) down++;
                    float frac = up + down == 0 ? 0.5f : up / (float)(up + down);   // 0 = top of the hole, 1 = bottom
                    Color32 c = Color32.Lerp(HoleTop, HoleBottom, frac);
                    float edge = Mathf.Clamp01((d - 1) / 2f);                // 0 at the edge, 1 inside
                    float shade = 0.62f + 0.38f * edge;                      // soft inner shadow, no outline
                    c = new Color32((byte)(c.r * shade), (byte)(c.g * shade), (byte)(c.b * shade), c.a);
                    bool corner = (!IsDug(x - 1, y) || !IsDug(x + 1, y)) && (!IsDug(x, y - 1) || !IsDug(x, y + 1));
                    if (d == 1) c.a = (byte)(corner ? 120 : 205);            // softly feathered edge
                    SetBlock(x, y, c);
                }
            }

            // 3. the hole carries on down to the next surface, and crumbs of dirt gather at the lip
            for (int y = Mathf.Max(0, y0 - 1); y < Mathf.Min(Height, y1 + 1); y++)
            {
                for (int x = rx0; x < rx1; x++)
                {
                    int i = y * Width + x;
                    if (!dug[i]) continue;

                    if (!IsDug(x, y + 1) && y + 1 < Height && planes[(y + 1) * Width + x] == 0)
                    {
                        if (Noise(x, y) % 3 != 0) Decorate(x, y + 1, Crumb1);
                        if (Noise(x + 7, y) % 4 == 0) Decorate(x, y + 2, Crumb2);
                        if (!IsDug(x - 1, y))                                // a little mound at each end of the lip
                        {
                            Decorate(x - 1, y + 1, Crumb1); Decorate(x - 2, y + 1, Crumb2); Decorate(x - 1, y + 2, Crumb2);
                        }
                        if (!IsDug(x + 1, y))
                        {
                            Decorate(x + 1, y + 1, Crumb1); Decorate(x + 2, y + 1, Crumb2); Decorate(x + 1, y + 2, Crumb2);
                        }
                    }

                    if (!IsDug(x, y - 1) && y - 1 >= 0 && planes[(y - 1) * Width + x] == 0)
                    {
                        int dl = 0, dr = 0;                                  // how far this column is from the sides of the hole
                        while (dl < 4 && IsDug(x - dl - 1, y)) dl++;
                        while (dr < 4 && IsDug(x + dr + 1, y)) dr++;
                        float edge = Mathf.Clamp01((Mathf.Min(dl, dr)) / 2f);
                        float shade = 0.62f + 0.38f * edge;
                        int landed = -1;                                       // the row of the surface it lands on, if any
                        int len = 0;                                           // how far the shaft runs through empty space
                        while (len < ShaftMax)
                        {
                            int yy = y - len - 1;
                            if (yy < 0) break;
                            int j = yy * Width + x;
                            if (planes[j] != 0 || dug[j]) { landed = yy; break; }   // reached the next surface
                            len++;
                        }
                        for (int k = 1; k <= len; k++)
                        {
                            int yy = y - k;
                            // darkest at the hole, softly fading away as it carries on down
                            float t = k / (float)(len + 1);
                            float alpha = 40f + 150f * Mathf.Pow(1f - t, 0.9f);
                            var c = Color32.Lerp(HoleTop, HoleBottom, 0.25f + 0.5f * t);
                            c = new Color32((byte)(c.r * shade), (byte)(c.g * shade), (byte)(c.b * shade), (byte)alpha);
                            deco[yy * Width + x] = true;
                            SetBlock(x, yy, c);
                        }
                        if (landed >= 0 && len > 0 && landed < Height - 1 && planes[landed * Width + x] != 0)
                        {
                            // a little pile of fallen dirt where the hole lands on the next surface
                            int h = 1 + (Noise(x, y) % 3 == 0 ? 1 : 0) + (dl > 1 && dr > 1 ? 1 : 0);
                            for (int m = 1; m <= h; m++) Decorate(x, landed + m, m == h ? Crumb2 : Crumb1, true);
                        }
                    }
                }
            }
            needsRedraw = true;
        }

        // A decoration pixel (only drawn on empty ground that isn't part of the picture's collision)
        void Decorate(int x, int y, Color32 c, bool overShaft = false)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;
            int i = y * Width + x;
            if (planes[i] != 0 || dug[i] || (deco[i] && !overShaft)) return;
            deco[i] = true;
            SetBlock(x, y, c);
        }

        bool IsDug(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height && dug[y * Width + x];

        void SetBlock(int x, int y, Color32 c)
        {
            int k = ArtScale;
            for (int sy = 0; sy < k; sy++)
            {
                int row = (y * k + sy) * texW + x * k;
                for (int sx = 0; sx < k; sx++) pixels[row + sx] = c;
            }
        }

        // Is there any stone in this rectangle (in the current plane)? (x1/y1 not included)
        public bool HasHardIn(int x0, int y0, int x1, int y1)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(Height, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(Width, x1); x++)
                {
                    int i = y * Width + x;
                    if (hard[i] && (planes[i] & ActivePlane) != 0) return true;
                }
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
