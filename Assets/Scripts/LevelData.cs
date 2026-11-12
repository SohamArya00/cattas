using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Cattas
{
    // One level, read from a text file in Assets/Resources/Levels (level01.txt, level02.txt, ...).
    //
    // The file has some settings at the top, then "map:" and a picture of the level drawn with characters.
    // Each character is an 8x8 pixel block:
    //   .  air            #  dirt (can be dug)        X  stone (can't be dug)
    //   /  dirt ramp going up to the right             \  dirt ramp going up to the left
    //   H  hatch (cats drop out of here)               E  exit (put ground under it!)
    // Something special placed in a level (hazards, switches, props, secrets).
    // In the level file:   object: <type> x0 y0 x1 y1 [extra...]     (game pixels, y measured UP from the bottom)
    //   water  x0 y0 x1 y1                    kittens that fall in get soaked (and go home to dry off)
    //   fire   x0 y0 x1 y1 <group>            singes kittens while lit
    //   switch x0 y0 x1 y1 <group> [art]      a kitten touching it turns off that group (e.g. the gas)
    //   prop   x0 y0 x1 y1 <art>              an object a kitten can knock over (secrets can hide behind it)
    //   secret x0 y0 x1 y1 <art> <Name...>    a hidden collectible
    public class LevelObject
    {
        public string type = "";
        public RectInt rect;
        public string group = "";
        public string art = "";
        public string title = "";
    }

    public class LevelData
    {
        public const int CellSize = 8;

        public string name = "Untitled";
        public string hint = "";
        public int cats = 10;
        public int need = 5;
        public int timeSeconds = 300;
        public float rate = 1.5f; // seconds between cats
        public int speed = 50;    // starting game speed in % (50 = normal). Players can only speed up from here.

        // Painted art (optional) - names of files in Assets/Resources/Art (without ".bytes").
        // foreground: the solid ground; see-through parts are air. 'X' map cells still mark stone,
        //             and H / E still place the hatch and exit. Other map characters are ignored.
        // background: the picture behind everything.
        public string background = "";
        public string foreground = "";
        public bool showMarkers = true; // draw the plain hatch/exit boxes (turn off when the art has its own)
        public int parSeconds;          // optional "finish in under" target time
        public readonly List<LevelObject> objects = new List<LevelObject>();
        public readonly int[] traits = new int[CatTraits.Count];
        public readonly List<string> rows = new List<string>();

        public int Cols { get; private set; }
        public int Rows { get; private set; }
        // Level size in game pixels: from the map, unless "size: <width> <height>" is given (handy for painted levels)
        public int sizeW, sizeH;
        public int PixelWidth => sizeW > 0 ? sizeW : Cols * CellSize;
        public int PixelHeight => sizeH > 0 ? sizeH : Rows * CellSize;

        public static LevelData Load(int number) => Load($"level{number:00}");

        public static LevelData Load(string fileName)
        {
            var asset = Resources.Load<TextAsset>("Levels/" + fileName);
            return asset == null ? null : Parse(asset.text);
        }

        public static int CountLevels()
        {
            int n = 0;
            while (n < 999 && Resources.Load<TextAsset>($"Levels/level{n + 1:00}") != null) n++;
            return n;
        }

        public static LevelData Parse(string text)
        {
            var d = ParseRaw(text);
            d.speed = Mathf.Clamp(d.speed, 1, 100);
            return d;
        }

        static LevelData ParseRaw(string text)
        {
            var d = new LevelData();
            bool inMap = false;
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                if (inMap) { d.rows.Add(raw); continue; }

                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//")) continue;
                if (line.ToLowerInvariant() == "map:") { inMap = true; continue; }

                int colon = line.IndexOf(':');
                if (colon < 0) continue;
                string key = line.Substring(0, colon).Trim().ToLowerInvariant();
                string val = line.Substring(colon + 1).Trim();
                switch (key)
                {
                    case "name": d.name = val; break;
                    case "hint": d.hint = val; break;
                    case "cats": int.TryParse(val, out d.cats); break;
                    case "need": int.TryParse(val, out d.need); break;
                    case "time": int.TryParse(val, out d.timeSeconds); break;
                    case "rate": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d.rate); break;
                    case "speed": int.TryParse(val, out d.speed); break;
                    case "par": int.TryParse(val, out d.parSeconds); break;
                    case "size":
                        var wh = val.Split(new[] { ' ', 'x', 'X', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (wh.Length == 2) { int.TryParse(wh[0], out d.sizeW); int.TryParse(wh[1], out d.sizeH); }
                        break;
                    case "object": ParseObject(d, val); break;
                    case "background": d.background = val; break;
                    case "foreground": d.foreground = val; break;
                    case "markers": d.showMarkers = !(val.ToLowerInvariant() == "off" || val.ToLowerInvariant() == "hidden" || val.ToLowerInvariant() == "no"); break;
                    case "traits": ParseTraits(d, val); break;
                }
            }

            while (d.rows.Count > 0 && d.rows[d.rows.Count - 1].Trim().Length == 0)
                d.rows.RemoveAt(d.rows.Count - 1);
            d.Rows = d.rows.Count;
            int cols = 0;
            foreach (var r in d.rows) cols = Mathf.Max(cols, r.Length);
            d.Cols = cols;
            return d;
        }

        static void ParseObject(LevelData d, string val)
        {
            var parts = val.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) return;
            var o = new LevelObject { type = parts[0].ToLowerInvariant() };
            if (!int.TryParse(parts[1], out int x0) || !int.TryParse(parts[2], out int y0) ||
                !int.TryParse(parts[3], out int x1) || !int.TryParse(parts[4], out int y1)) return;
            o.rect = new RectInt(Mathf.Min(x0, x1), Mathf.Min(y0, y1), Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            switch (o.type)
            {
                case "fire":
                    if (parts.Length > 5) o.group = parts[5];
                    break;
                case "switch":
                    if (parts.Length > 5) o.group = parts[5];
                    if (parts.Length > 6) o.art = parts[6];
                    break;
                case "prop":
                    if (parts.Length > 5) o.art = parts[5];
                    break;
                case "secret":
                    if (parts.Length > 5) o.art = parts[5];
                    o.title = parts.Length > 6 ? string.Join(" ", parts, 6, parts.Length - 6) : "a secret";
                    break;
            }
            d.objects.Add(o);
        }

        // e.g. "LitterLord=3, Fluffchute=2"
        static void ParseTraits(LevelData d, string val)
        {
            foreach (var part in val.Split(','))
            {
                var kv = part.Split('=');
                if (kv.Length != 2) continue;
                if (Enum.TryParse(kv[0].Trim(), true, out CatTrait trait) && int.TryParse(kv[1].Trim(), out int n))
                    d.traits[(int)trait] = n;
            }
        }

        // The character at a map position (row 0 is the TOP line of the map).
        public char Cell(int col, int rowFromTop)
        {
            if (rowFromTop < 0 || rowFromTop >= Rows) return '.';
            var r = rows[rowFromTop];
            return col >= 0 && col < r.Length ? r[col] : '.';
        }
    }
}
