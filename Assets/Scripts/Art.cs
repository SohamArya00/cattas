using UnityEngine;

namespace Cattas
{
    // Loads painted art from Assets/Resources/Art.
    //
    // Art files are PNG images renamed to end in ".bytes" (e.g. kitchen_bg.bytes).
    // That way Unity keeps them exactly as painted (full size, no compression) and the game can read them.
    public static class Art
    {
        public static Texture2D Load(string name, bool smooth = true)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var data = Resources.Load<TextAsset>("Art/" + name);
            if (data == null)
            {
                Debug.LogWarning($"Cattas: art '{name}' not found (expected Assets/Resources/Art/{name}.bytes)");
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(data.bytes, false))
            {
                Debug.LogWarning($"Cattas: art '{name}' isn't a valid PNG/JPG image");
                Object.Destroy(tex);
                return null;
            }
            tex.filterMode = smooth ? FilterMode.Bilinear : FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        // Cut a sprite sheet (frames side by side) into separate sprites.
        public static Sprite[] SliceSheet(Texture2D sheet, int frameWidth, float pixelsPerUnit, Vector2 pivot)
        {
            if (sheet == null || frameWidth <= 0) return null;
            int n = sheet.width / frameWidth;
            var frames = new Sprite[n];
            for (int i = 0; i < n; i++)
                frames[i] = Sprite.Create(sheet, new Rect(i * frameWidth, 0, frameWidth, sheet.height), pivot, pixelsPerUnit);
            return frames;
        }

        // A soft rounded rectangle, used for nicer-looking panels and buttons.
        public static Texture2D RoundedBox(Color fill, Color border, int size = 32, float radius = 11f, float borderWidth = 2f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // distance from the rounded-rectangle edge (negative = inside)
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float a = Mathf.Clamp01(0.5f - outside);
                    Color c = outside > -borderWidth ? border : fill;
                    c.a *= a;
                    px[y * size + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
