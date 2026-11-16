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
    //   H  hatch (cats drop out of here, walking right)   h  hatch (cats walk LEFT)
    //   E  exit (put ground under it!)
    // Something special placed in a level (hazards, switches, props, secrets).
    // In the level file:   object: <type> x0 y0 x1 y1 [extra...]     (game pixels, y measured UP from the bottom)
    //   water  x0 y0 x1 y1                    kittens that fall in get soaked (and go home to dry off)
    //   fire   x0 y0 x1 y1 <group>            singes kittens while lit
    //   switch x0 y0 x1 y1 <group> [art]      a kitten touching it turns off that group (e.g. the gas)
    //   prop   x0 y0 x1 y1 <art>              an object a kitten can knock over (secrets can hide behind it)
    //   trap   x0 y0 x1 y1 <art>              a mouse trap: SNAP! (bridge over it, or ZAP it to disarm)
    //   secret x0 y0 x1 y1 <art> <Name...>    a hidden collectible
    // A curved walkway kittens follow (ellipse: centre cx,cy, radii a,b, all in level pixels, y up).
    // Closed = a full loop (tub rim, toilet lid); open = an arc between two angles in degrees (0 = right end, 90 = top/back).
    public class RailPath
    {
        public string name = "";
        public float cx, cy, a = 1f, b = 1f;
        public bool closed = true;
        public float deg0, deg1 = 360f;
        public int frontLane, backLane;       // depth lanes (bits) of the front half and the back half (0 = leave the kitten's lane alone)
        public Vector2 Point(float t) => new Vector2(cx + a * Mathf.Cos(t), cy + b * Mathf.Sin(t));
        // length of path per unit of angle (so a kitten moves 1 pixel of arc per step)
        public float Speed(float t) => Mathf.Sqrt(a * a * Mathf.Sin(t) * Mathf.Sin(t) + b * b * Mathf.Cos(t) * Mathf.Cos(t));
    }

    // A thing sitting on a path (faucet, vase...). While it is still solid, kittens on the path turn back at it.
    public class RailBlock { public RectInt rect; public string path = ""; public int side; }   // side: 0 = any, 1 = back edge only, -1 = front edge only

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
        public string stone = "";       // optional picture: its opaque parts can't be dug (painted levels)

        // COLLISION-ONLY painted levels: the level picture (background) is never touched. This data picture says what is
        // solid (opaque = solid, red = back plane, green = front plane, neither = both planes).
        public string collision = "";
        // OCCLUSION: a data picture outlining things kittens in the back plane walk BEHIND (e.g. the stool). The game
        // re-draws exactly those pixels of the (untouched) level picture in front of those kittens.
        public string occlusion = "";
        // Kittens that land at or below this height join the front plane (0 = no depth planes)
        public int frontPlaneY;
        // "lanejump: on": kittens can stand on surfaces from any depth lane (and move to that lane). "startlane: m|b|f".
        public bool laneJump;
        public int startLane;
        // The part of the picture the camera may show (e.g. to leave out a strip). Empty = the whole picture.
        public RectInt view;
        public bool HasView => view.width > 0 && view.height > 0;
        // Explicit hatches / goal zone / mother cat spot (instead of H / E markers in the text map)
        public class HatchSpot { public int x, y, dir = 1; }
        public readonly List<HatchSpot> hatches = new List<HatchSpot>();
        public RectInt goal;
        public bool HasGoal => goal.width > 0 && goal.height > 0;
        public bool hasMum;
        public Vector2Int mum;
        public int mumFacing = -1;

        // Tutorial tips: "tip: <when> | <text>"  -  when = start, below <y>, saved <n>, used <Trait> <n>
        public class Tip { public string when = "start"; public string arg = ""; public int n; public string text = ""; }
        public readonly List<Tip> tips = new List<Tip>();
        public bool showMarkers = true; // draw the plain hatch/exit boxes (turn off when the art has its own)
        public int parSeconds;          // optional "finish in under" target time
        public readonly List<LevelObject> objects = new List<LevelObject>();
        public readonly List<RailPath> paths = new List<RailPath>();
        public readonly List<RailBlock> railBlocks = new List<RailBlock>();
        public readonly int[] traits = new int[CatTraits.Count];
        public readonly List<string> rows = new List<string>();

        public int Cols { get; private set; }
        public int Rows { get; private set; }
        // Level size in game pixels: from the map, unless "size: <width> <height>" is given (handy for painted levels)
        public int sizeW, sizeH;

        // The NG0 play area inside a bigger picture: "area: x0 y0 x1 y1" (level pixels, y up). Empty = whole level.
        public RectInt area;
        public bool HasArea => area.width > 0 && area.height > 0;

        // What the exit looks like: "house" (plain box), "mother" (mother cat with her milk), "litter" (litter tray)
        public string exitStyle = "house";
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
                    case "exit": d.exitStyle = val.ToLowerInvariant(); break;
                    case "area":
                        var a = val.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (a.Length == 4 && int.TryParse(a[0], out int ax0) && int.TryParse(a[1], out int ay0) &&
                            int.TryParse(a[2], out int ax1) && int.TryParse(a[3], out int ay1))
                            d.area = new RectInt(Mathf.Min(ax0, ax1), Mathf.Min(ay0, ay1), Mathf.Abs(ax1 - ax0), Mathf.Abs(ay1 - ay0));
                        break;
                    case "size":
                        var wh = val.Split(new[] { ' ', 'x', 'X', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (wh.Length == 2) { int.TryParse(wh[0], out d.sizeW); int.TryParse(wh[1], out d.sizeH); }
                        break;
                    case "object": ParseObject(d, val); break;
                    case "background": d.background = val; break;
                    case "stone": d.stone = val; break;
                    case "collision": d.collision = val; break;
                    case "occlusion": d.occlusion = val; break;
                    case "lanejump": d.laneJump = val.ToLowerInvariant() == "on" || val.ToLowerInvariant() == "yes"; break;
                    case "startlane": d.startLane = LaneBit(val); break;
                    case "frontplane": int.TryParse(val, out d.frontPlaneY); break;
                    case "view": d.view = ParseRect(val); break;
                    case "goal": d.goal = ParseRect(val); break;
                    case "hatch":
                    {
                        var h = val.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (h.Length >= 2 && int.TryParse(h[0], out int hx) && int.TryParse(h[1], out int hy))
                        {
                            var spot = new HatchSpot { x = hx, y = hy };
                            if (h.Length > 2 && int.TryParse(h[2], out int hd)) spot.dir = hd < 0 ? -1 : 1;
                            d.hatches.Add(spot);
                        }
                        break;
                    }
                    case "path":
                    {
                        // path: <name> <cx> <cy> <a> <b> closed|open [deg0 deg1]
                        var q = val.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (q.Length >= 6)
                        {
                            var rp = new RailPath { name = q[0] };
                            float.TryParse(q[1], NumberStyles.Float, CultureInfo.InvariantCulture, out rp.cx);
                            float.TryParse(q[2], NumberStyles.Float, CultureInfo.InvariantCulture, out rp.cy);
                            float.TryParse(q[3], NumberStyles.Float, CultureInfo.InvariantCulture, out rp.a);
                            float.TryParse(q[4], NumberStyles.Float, CultureInfo.InvariantCulture, out rp.b);
                            rp.closed = q[5].ToLowerInvariant() != "open";
                            int next = 6;
                            if (!rp.closed && q.Length >= 8)
                            {
                                float.TryParse(q[6], NumberStyles.Float, CultureInfo.InvariantCulture, out rp.deg0);
                                float.TryParse(q[7], NumberStyles.Float, CultureInfo.InvariantCulture, out rp.deg1);
                                next = 8;
                            }
                            if (q.Length > next + 1) { rp.frontLane = LaneBit(q[next]); rp.backLane = LaneBit(q[next + 1]); }
                            d.paths.Add(rp);
                        }
                        break;
                    }
                    case "railblock":
                    {
                        // railblock: <x0> <y0> <x1> <y1> [path name] [back|front]
                        var q = val.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (q.Length >= 4)
                        {
                            var r = ParseRect(string.Join(" ", q[0], q[1], q[2], q[3]));
                            if (r.width > 0 && r.height > 0)
                                d.railBlocks.Add(new RailBlock
                                {
                                    rect = r,
                                    path = q.Length > 4 ? q[4] : "",
                                    side = q.Length > 5 ? (q[5].ToLowerInvariant() == "back" ? 1 : q[5].ToLowerInvariant() == "front" ? -1 : 0) : 0
                                });
                        }
                        break;
                    }
                    case "mum":
                    {
                        var m = val.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (m.Length >= 2 && int.TryParse(m[0], out int mx) && int.TryParse(m[1], out int my))
                        {
                            d.hasMum = true;
                            d.mum = new Vector2Int(mx, my);
                            if (m.Length > 2 && int.TryParse(m[2], out int mf)) d.mumFacing = mf < 0 ? -1 : 1;
                        }
                        break;
                    }
                    case "tip":
                    {
                        int bar = val.IndexOf('|');
                        if (bar < 0) break;
                        var cond = val.Substring(0, bar).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        var tip = new Tip { text = val.Substring(bar + 1).Trim() };
                        if (cond.Length > 0) tip.when = cond[0].ToLowerInvariant();
                        if (cond.Length > 1 && !int.TryParse(cond[1], out tip.n)) tip.arg = cond[1];
                        if (cond.Length > 2) int.TryParse(cond[2], out tip.n);
                        d.tips.Add(tip);
                        break;
                    }
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

        public static int LaneBit(string s)
        {
            switch (s.ToLowerInvariant())
            {
                case "b": case "back": return 1;
                case "f": case "front": return 2;
                case "m": case "mid": return 4;
                default: return 0;
            }
        }

        static RectInt ParseRect(string val)
        {
            var a = val.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 4 && int.TryParse(a[0], out int x0) && int.TryParse(a[1], out int y0) &&
                int.TryParse(a[2], out int x1) && int.TryParse(a[3], out int y1))
                return new RectInt(Mathf.Min(x0, x1), Mathf.Min(y0, y1), Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            return new RectInt();
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
                case "trap":
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
