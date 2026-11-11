using UnityEngine;

namespace Cattas
{
    // Makes simple placeholder pictures from code, so we don't need any art files yet.
    public static class SpriteMaker
    {
        public static Sprite FromArt(string[] rows, Vector2 pivot)
        {
            int h = rows.Length, w = rows[0].Length;
            var pixels = new Color32[w * h];
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                    pixels[(h - 1 - r) * w + c] = ColourFor(rows[r][c]);
            return Build(w, h, pixels, pivot);
        }

        public static Sprite Solid(int w, int h, Color32 colour, Vector2 pivot)
        {
            var pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = colour;
            return Build(w, h, pixels, pivot);
        }

        public static Sprite Circle(int radius, Color32 colour)
        {
            int size = radius * 2 + 1;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int dx = x - radius, dy = y - radius;
                    pixels[y * size + x] = dx * dx + dy * dy <= radius * radius ? colour : new Color32(0, 0, 0, 0);
                }
            return Build(size, size, pixels, new Vector2(0.5f, 0.5f));
        }

        static Sprite Build(int w, int h, Color32[] pixels, Vector2 pivot)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, 1f);
        }

        static Color32 ColourFor(char ch)
        {
            switch (ch)
            {
                case 'w': return new Color32(255, 255, 255, 255);
                case 'k': return new Color32(20, 20, 20, 255);
                case 'p': return new Color32(255, 150, 170, 255);
                default: return new Color32(0, 0, 0, 0);
            }
        }
    }
}
