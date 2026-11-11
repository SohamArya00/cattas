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
        public readonly int[] traits = new int[CatTraits.Count];
        public readonly List<string> rows = new List<string>();

        public int Cols { get; private set; }
        public int Rows { get; private set; }
        public int PixelWidth => Cols * CellSize;
        public int PixelHeight => Rows * CellSize;

        public static LevelData Load(int number)
        {
            var asset = Resources.Load<TextAsset>($"Levels/level{number:00}");
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
