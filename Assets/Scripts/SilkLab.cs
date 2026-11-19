using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Cattas
{
    // SILK LAB - the 2.5D test world (Doll House picture).
    //
    // Kittens don't live on a flat strip here. They follow ROUTES drawn through the picture:
    //   LOOP    a closed route round the top dais tier: front lip, round the vanity end, BEHIND the bath and planter, home again.
    //   TUNNEL  Zoomies dig through the vanity along the drawers (hidden inside!) and pop out of the hole in the end panel.
    //   FLOOR   the floor in front of the vanity; kittens that drop out of the hole walk it and turn round at the ends.
    //
    // How it works (the "back end"):
    //  * Each route is a smooth curve through control points traced from the picture (Catmull-Rom spline).
    //  * Kittens are placed by WORLD distance along a route, measured with perspective: things further up the picture are
    //    smaller, and movement into the screen is squashed (depth_squash) so corners take the time they really would.
    //  * Positions are evaluated analytically every frame (no pixel stepping): smooth at any refresh rate or resolution.
    //  * Draw order: picture, kittens BEHIND (z=1), bath/planter re-drawn from the picture, kittens in FRONT.
    public class SilkLab : MonoBehaviour
    {
        const int OrderBackground = -10, OrderBehind = 4, OrderOccluder = 6, OrderFront = 8, OrderFx = 12;

        Camera cam;
        CatRig rig;
        System.Action onExit;

        float picW, picH, kittenScale = 3f, baseSpeed = 70f, zoomSpeed = 190f, squash = 0.3f, spawnGap = 1.3f;
        int count = 10;
        Vector2 planter = new Vector2(505f, 520f), dive = new Vector2(1121f, 624f), hole = new Vector2(1545f, 648f);
        readonly List<Vector3> loopPts = new List<Vector3>(), tunnelPts = new List<Vector3>(), floorPts = new List<Vector3>();

        // A route = dense samples + cumulative WORLD distance
        class Route
        {
            public Vector3[] s;          // x, y (picture px, y down), z (0 front .. 1 behind)
            public float[] d;            // world distance at each sample
            public float total;
            public bool closed;

            public void Build(List<Vector3> controls, bool isClosed, float squash)
            {
                closed = isClosed;
                const int perSegment = 48;
                int n = controls.Count;
                var pts = new List<Vector3>();
                int segs = closed ? n : n - 1;
                for (int i = 0; i < segs; i++)
                {
                    Vector3 a = controls[closed ? (i - 1 + n) % n : Mathf.Max(0, i - 1)], b = controls[i];
                    Vector3 c = controls[closed ? (i + 1) % n : i + 1], e = controls[closed ? (i + 2) % n : Mathf.Min(n - 1, i + 2)];
                    for (int k = 0; k < perSegment; k++) pts.Add(CatmullRom(a, b, c, e, k / (float)perSegment));
                }
                if (!closed) pts.Add(controls[n - 1]);
                s = pts.ToArray();
                d = new float[s.Length + 1];
                int links = closed ? s.Length : s.Length - 1;
                for (int i = 0; i < links; i++)
                {
                    var p = s[i]; var q = s[(i + 1) % s.Length];
                    float sc = Scale((p.y + q.y) * 0.5f);
                    float dx = (q.x - p.x) / sc, dy = (q.y - p.y) / (sc * squash);      // sideways = true length; into the screen = squashed
                    d[i + 1] = d[i] + Mathf.Sqrt(dx * dx + dy * dy);
                }
                total = d[links];
            }

            // position (picture px), z and direction of travel at world distance w
            public void At(float w, out Vector3 pos, out Vector2 dir)
            {
                w = closed ? Mathf.Repeat(w, total) : Mathf.Clamp(w, 0f, total);
                int links = closed ? s.Length : s.Length - 1;
                int lo = 0, hi = links;                           // last sample with d <= w
                while (hi - lo > 1) { int mid = (lo + hi) / 2; if (d[mid] <= w) lo = mid; else hi = mid; }
                int next = (lo + 1) % s.Length;
                float span = d[lo + 1] - d[lo];
                float t = span > 1e-5f ? Mathf.Clamp01((w - d[lo]) / span) : 0f;
                pos = Vector3.Lerp(s[lo], s[next], t);
                dir = new Vector2(s[next].x - s[lo].x, s[next].y - s[lo].y);
            }

            // world distance of the sample whose x is closest to x (for routes that run mostly sideways)
            public float DistanceAtX(float x)
            {
                int best = 0; float bd = float.MaxValue;
                for (int i = 0; i < s.Length; i++)
                {
                    float dd = Mathf.Abs(s[i].x - x);
                    if (dd < bd) { bd = dd; best = i; }
                }
                return d[best];
            }

            public float DistanceNear(float x, float y)
            {
                int best = 0; float bd = float.MaxValue;
                for (int i = 0; i < s.Length; i++)
                {
                    float dd = (s[i].x - x) * (s[i].x - x) + (s[i].y - y) * (s[i].y - y);
                    if (dd < bd) { bd = dd; best = i; }
                }
                return d[best];
            }
        }

        Route loop = new Route(), tunnel = new Route(), floor = new Route();
        float diveW;

        enum Mode { Drop, Loop, Tunnel, Pop, Floor }

        class Walker
        {
            public Transform wrapper;
            public CatPuppet puppet;
            public Mode mode = Mode.Drop;
            public float w;              // world distance along the current route
            public int dir = 1;          // +1 / -1 along open routes
            public float speedMul = 1f;
            public bool flip, zoomies;
            public float fallY, fallVel, fallX, fallTo;
        }

        class Puff { public Transform t; public SpriteRenderer sr; public Vector2 vel; public float life, age, size; }

        Transform root;
        LineRenderer loopLine, tunnelLine, floorLine;
        readonly List<Texture2D> textures = new List<Texture2D>();
        readonly List<Walker> walkers = new List<Walker>();
        readonly List<Puff> puffs = new List<Puff>();
        Sprite puffSprite;
        float spawnTimer;
        int spawned, zoomiesLeft = 5;
        float timeScale = 1f;
        bool paused, showPath, armedZoomies;
        Rect panelRect;
        string pictureName = "level3_bg", occluderName = "level3_occl";

        public void Init(Camera camera, CatRig kittenRig, System.Action exit)
        {
            cam = camera;
            rig = kittenRig;
            onExit = exit;
            if (!Load("Levels/silk_dollhouse")) { Debug.LogError("Cattas: Silk Lab data missing"); Exit(); return; }
            BuildScene();
        }

        // ---------- data ----------
        static float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        bool Load(string resource)
        {
            var file = Resources.Load<TextAsset>(resource);
            if (file == null) return false;
            foreach (var raw in file.text.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//")) continue;
                var p = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                switch (p[0])
                {
                    case "picture": pictureName = p[1]; break;
                    case "occluder": occluderName = p[1]; break;
                    case "size": picW = F(p[1]); picH = F(p[2]); break;
                    case "kitten_scale": kittenScale = F(p[1]); break;
                    case "speed": baseSpeed = F(p[1]); break;
                    case "zoom_speed": zoomSpeed = F(p[1]); break;
                    case "depth_squash": squash = Mathf.Max(0.05f, F(p[1])); break;
                    case "count": count = int.Parse(p[1], CultureInfo.InvariantCulture); break;
                    case "spawn_gap": spawnGap = F(p[1]); break;
                    case "planter": planter = new Vector2(F(p[1]), F(p[2])); break;
                    case "dive": dive = new Vector2(F(p[1]), F(p[2])); break;
                    case "hole": hole = new Vector2(F(p[1]), F(p[2])); break;
                    case "p": loopPts.Add(new Vector3(F(p[1]), F(p[2]), F(p[3]))); break;
                    case "t": tunnelPts.Add(new Vector3(F(p[1]), F(p[2]), F(p[3]))); break;
                    case "f": floorPts.Add(new Vector3(F(p[1]), F(p[2]), F(p[3]))); break;
                }
            }
            return loopPts.Count >= 4 && picW > 0 && picH > 0;
        }

        // perspective size of a kitten at screen height y (further up the screen = further away = smaller)
        static float Scale(float y) => Mathf.Clamp(0.9f + (y - 585f) * 0.00277f, 0.85f, 1.5f);

        static Vector3 CatmullRom(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * b) + (-a + c) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (-a + 3f * b - 3f * c + d) * t3);
        }

        // ---------- scene ----------
        void BuildScene()
        {
            loop.Build(loopPts, true, squash);
            if (tunnelPts.Count >= 2) tunnel.Build(tunnelPts, false, squash);
            if (floorPts.Count >= 2) floor.Build(floorPts, false, squash);
            diveW = loop.DistanceNear(dive.x, dive.y);
            puffSprite = SpriteMaker.Circle(12, new Color32(255, 255, 255, 255));

            root = new GameObject("Silk Lab").transform;
            root.SetParent(transform, false);

            var bg = Art.Load(pictureName);
            if (bg != null)
            {
                textures.Add(bg);
                var go = new GameObject("Doll House picture");
                go.transform.SetParent(root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Sprite.Create(bg, new Rect(0, 0, bg.width, bg.height), Vector2.zero, 1f);
                sr.material = GameBootstrap.SpriteMaterial;
                sr.sortingOrder = OrderBackground;
                MakeOccluder(bg);
            }

            loopLine = MakeLine("Loop line", loop, new Color(1f, 1f, 0.2f, 0.9f), true);
            tunnelLine = MakeLine("Tunnel line", tunnel, new Color(1f, 0.2f, 1f, 0.9f), false);
            floorLine = MakeLine("Floor line", floor, new Color(0.2f, 0.85f, 1f, 0.9f), false);

            // fit the whole picture on screen
            float aspect = Mathf.Max(0.5f, Screen.width / (float)Screen.height);
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(picH / 2f, picW / 2f / aspect);
            cam.transform.position = new Vector3(picW / 2f, picH / 2f, -10f);
            cam.backgroundColor = new Color(0.1f, 0.07f, 0.06f);
        }

        LineRenderer MakeLine(string name, Route r, Color colour, bool isLoop)
        {
            if (r.s == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = colour;
            lr.widthMultiplier = 2f;
            lr.loop = isLoop;
            lr.sortingOrder = 20;
            var pts = new List<Vector3>();
            for (int i = 0; i < r.s.Length; i += 4) pts.Add(new Vector3(r.s[i].x, picH - r.s[i].y, 0f));
            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());
            lr.enabled = false;
            return lr;
        }

        // Re-draw the bath and planter pixels from the picture on top of the "behind" kittens
        void MakeOccluder(Texture2D picture)
        {
            var mask = Art.Load(occluderName);
            if (mask == null) return;
            int pw = picture.width, ph = picture.height;
            if (mask.width != pw || mask.height != ph) { Destroy(mask); return; }
            var pic = picture.GetPixels32();
            var m = mask.GetPixels32();
            Destroy(mask);
            int minX = pw, minY = ph, maxX = -1, maxY = -1;
            for (int y = 0; y < ph; y++)
                for (int x = 0; x < pw; x++)
                    if (m[y * pw + x].a > 0)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
            if (maxX < 0) return;
            int w = maxX - minX + 1, h = maxY - minY + 1;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = pic[(y + minY) * pw + (x + minX)];
                    c.a = m[(y + minY) * pw + (x + minX)].a;
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            textures.Add(tex);
            var go = new GameObject("Bath and planter (kittens walk behind)");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(minX, minY, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, 1f);
            sr.material = GameBootstrap.SpriteMaterial;
            sr.sortingOrder = OrderOccluder;
        }

        // ---------- kittens ----------
        void Spawn()
        {
            if (rig == null) return;
            var wrapper = new GameObject("Kitten").transform;
            wrapper.SetParent(root, false);
            var puppet = CatPuppet.Create(wrapper, rig, Color.white, OrderBehind);
            puppet.state = CatState.Falling;
            puppet.moveScale = 1f / kittenScale;
            var wk = new Walker { wrapper = wrapper, puppet = puppet, mode = Mode.Drop, fallY = planter.y, fallVel = 0f };
            wk.speedMul = 1f + ((spawned * 37) % 11 - 5) * 0.012f;       // each kitten has a very slightly different pace
            Vector3 p; Vector2 dr;
            loop.At(0f, out p, out dr);
            wk.fallX = p.x; wk.fallTo = p.y;                              // drop out of the planter's mouth onto the back of the tier
            walkers.Add(wk);
            spawned++;
            Place(wk);
        }

        void Place(Walker k)
        {
            Vector3 pos; Vector2 dir;
            switch (k.mode)
            {
                case Mode.Drop:
                case Mode.Pop:
                {
                    var p = new Vector3(k.fallX, k.fallY, k.mode == Mode.Drop ? 1f : 0f);
                    k.puppet.state = CatState.Falling;
                    Apply(k, p, p.z, Scale(p.y), k.flip);
                    return;
                }
                case Mode.Loop:
                    loop.At(k.w, out pos, out dir);
                    break;
                case Mode.Tunnel:
                    tunnel.At(k.w, out pos, out dir);
                    Apply(k, pos, 0f, Scale(pos.y), false);
                    return;
                default:
                    floor.At(k.w, out pos, out dir);
                    dir *= k.dir;
                    break;
            }
            // face the way we're walking; near corners (mostly into/out of the screen) keep the last facing
            if (Mathf.Abs(dir.x) > 0.25f * dir.magnitude) k.flip = dir.x < 0f;
            k.puppet.state = k.zoomies ? CatState.Zooming : CatState.Walking;
            Apply(k, pos, Mathf.Clamp01(pos.z), Scale(pos.y), k.flip);
        }

        void Apply(Walker k, Vector3 pos, float z, float scale, bool flip)
        {
            if (!k.wrapper.gameObject.activeSelf) k.wrapper.gameObject.SetActive(true);
            k.wrapper.position = new Vector3(pos.x, picH - pos.y, 0f);
            k.wrapper.localScale = Vector3.one * (kittenScale * scale);
            k.puppet.flip = flip;
            k.puppet.SetOrder(z > 0.5f ? OrderBehind : OrderFront);
            float shade = Mathf.Lerp(1f, 0.86f, z);                // a touch darker when further back
            k.puppet.SetColor(new Color(shade, shade, shade, 1f));
        }

        void Step(Walker k, float dt)
        {
            switch (k.mode)
            {
                case Mode.Drop:
                    k.fallVel += 900f * dt;
                    k.fallY += k.fallVel * dt;
                    if (k.fallY >= k.fallTo) { k.mode = Mode.Loop; k.w = 0f; }
                    break;
                case Mode.Loop:
                {
                    float before = Mathf.Repeat(k.w - diveW, loop.total);
                    k.w += baseSpeed * k.speedMul * (k.zoomies ? 2.2f : 1f) * dt;
                    float after = Mathf.Repeat(k.w - diveW, loop.total);
                    // a Zoomies kitten that reaches the vanity digs straight into it
                    if (k.zoomies && tunnel.s != null && before > loop.total - 120f && after < before && after < 120f)
                    {
                        Dust(new Vector2(dive.x + 4f, dive.y - 10f), 14, 90f);
                        k.mode = Mode.Tunnel; k.w = 0f;
                        k.wrapper.gameObject.SetActive(false);                  // hidden inside the vanity
                    }
                    break;
                }
                case Mode.Tunnel:
                    k.w += zoomSpeed * dt;
                    if (k.w >= tunnel.total)
                    {
                        Dust(hole, 18, 140f);                                   // pop! out of the hole in the end panel
                        k.mode = Mode.Pop; k.fallX = hole.x; k.fallY = hole.y; k.fallVel = -120f;
                        floor.At(floor.DistanceAtX(hole.x), out var fp, out var fd);
                        k.fallTo = fp.y; k.flip = false;
                        k.wrapper.gameObject.SetActive(true);
                    }
                    break;
                case Mode.Pop:
                    k.fallVel += 900f * dt;
                    k.fallY += k.fallVel * dt;
                    if (k.fallY >= k.fallTo)
                    {
                        Dust(new Vector2(k.fallX, k.fallTo), 6, 60f);
                        k.mode = Mode.Floor; k.zoomies = false;
                        k.w = floor.DistanceAtX(k.fallX); k.dir = -1;    // set off along the floor towards the middle of the room
                    }
                    break;
                case Mode.Floor:
                    k.w += k.dir * baseSpeed * k.speedMul * dt;
                    if (k.w <= 0f) { k.w = 0f; k.dir = 1; }
                    else if (k.w >= floor.total) { k.w = floor.total; k.dir = -1; }
                    break;
            }
        }

        // little puffs of dust
        void Dust(Vector2 at, int n, float speed)
        {
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Puff");
                go.transform.SetParent(root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = puffSprite;
                sr.material = GameBootstrap.SpriteMaterial;
                sr.sortingOrder = OrderFx;
                float a = Random.Range(0f, Mathf.PI * 2f);
                var p = new Puff { t = go.transform, sr = sr, vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(0.3f, 1f) * speed, life = Random.Range(0.4f, 0.8f), size = Random.Range(0.5f, 1.2f) };
                go.transform.position = new Vector3(at.x, picH - at.y, 0f);
                puffs.Add(p);
            }
        }

        void UpdatePuffs(float dt)
        {
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                var p = puffs[i];
                p.age += dt;
                if (p.age >= p.life) { Destroy(p.t.gameObject); puffs.RemoveAt(i); continue; }
                float u = p.age / p.life;
                p.t.position += (Vector3)(p.vel * dt);
                p.vel *= 1f - 3f * dt;
                p.t.localScale = Vector3.one * (p.size * (1f + u * 1.8f));
                p.sr.color = new Color(1f, 0.9f, 0.75f, (1f - u) * 0.8f);
            }
        }

        void Update()
        {
            if (cam == null) return;
            ReadInput();
            if (paused) return;
            float dt = Time.deltaTime * timeScale;
            spawnTimer += dt;
            if (spawned < count && spawnTimer >= spawnGap) { spawnTimer = 0f; Spawn(); }
            foreach (var k in walkers) { Step(k, dt); Place(k); }
            UpdatePuffs(dt);
            if (loopLine != null) loopLine.enabled = showPath;
            if (tunnelLine != null) tunnelLine.enabled = showPath;
            if (floorLine != null) floorLine.enabled = showPath;
        }

        void ReadInput()
        {
            bool escape = false, click = false;
            Vector2 mouse = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var ms = UnityEngine.InputSystem.Mouse.current;
            if (kb != null) escape = kb.escapeKey.wasPressedThisFrame;
            if (ms != null) { click = ms.leftButton.wasPressedThisFrame; mouse = ms.position.ReadValue(); }
#else
            escape = Input.GetKeyDown(KeyCode.Escape);
            click = Input.GetMouseButtonDown(0);
            mouse = Input.mousePosition;
#endif
            if (escape) { Exit(); return; }
            if (!click || !armedZoomies || zoomiesLeft <= 0) return;
            if (panelRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y))) return;       // that click was on a button
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, -cam.transform.position.z));
            Walker best = null; float bestD = 60f;
            foreach (var k in walkers)
            {
                if (k.mode != Mode.Loop || k.zoomies) continue;
                float dx = k.wrapper.position.x - world.x, dy = k.wrapper.position.y + kittenScale * 5f - world.y;
                float dd = Mathf.Sqrt(dx * dx + dy * dy);
                if (dd < bestD) { bestD = dd; best = k; }
            }
            if (best == null) return;
            best.zoomies = true;
            zoomiesLeft--;
            Dust(new Vector2(best.wrapper.position.x, picH - best.wrapper.position.y - 10f), 8, 70f);
            if (zoomiesLeft <= 0) armedZoomies = false;
        }

        // ---------- screen controls ----------
        GUIStyle buttonStyle, labelStyle;

        void OnGUI()
        {
            if (root == null) return;
            if (buttonStyle == null)
            {
                int fs = Mathf.Max(14, Screen.height / 40);
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = fs };
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = fs };
                labelStyle.normal.textColor = Color.white;
            }
            float bh = buttonStyle.fontSize * 2.1f, bw = buttonStyle.fontSize * 11f, m = buttonStyle.fontSize * 0.6f;
            float x = m, y = m;
            GUI.Label(new Rect(x, y, bw * 3f, bh), "Silk Lab - Doll House (Esc = menu)", labelStyle); y += bh;
            if (GUI.Button(new Rect(x, y, bw, bh), "Back to menu", buttonStyle)) { Exit(); return; } y += bh + m * 0.5f;
            if (GUI.Button(new Rect(x, y, bw, bh), paused ? "Resume" : "Pause", buttonStyle)) paused = !paused; y += bh + m * 0.5f;
            if (GUI.Button(new Rect(x, y, bw, bh), showPath ? "Hide route lines" : "Show route lines", buttonStyle)) showPath = !showPath; y += bh + m * 0.5f;
            string zt = zoomiesLeft <= 0 ? "Zoomies: none left" : armedZoomies ? $"ZOOMIES ON - click a kitten ({zoomiesLeft})" : $"Zoomies ({zoomiesLeft} left)";
            if (GUI.Button(new Rect(x, y, bw * 1.5f, bh), zt, buttonStyle) && zoomiesLeft > 0) armedZoomies = !armedZoomies; y += bh + m * 0.5f;
            GUI.Label(new Rect(x, y, bw * 1.5f, bh), $"Speed x{timeScale:0.00}", labelStyle); y += bh * 0.8f;
            timeScale = GUI.HorizontalSlider(new Rect(x, y, bw * 1.4f, bh * 0.5f), timeScale, 0.1f, 3f); y += bh * 0.8f;
            if (GUI.Button(new Rect(x, y, bw, bh), "Restart kittens", buttonStyle)) Restart();
            y += bh;
            panelRect = new Rect(0, 0, bw * 1.5f + m * 2f, y + m);
        }

        void Restart()
        {
            foreach (var k in walkers) if (k.wrapper != null) Destroy(k.wrapper.gameObject);
            walkers.Clear();
            spawned = 0;
            zoomiesLeft = 5;
            armedZoomies = false;
            spawnTimer = spawnGap;
        }

        void Exit()
        {
            foreach (var t in textures) if (t != null) Destroy(t);
            textures.Clear();
            if (root != null) Destroy(root.gameObject);
            root = null;
            onExit?.Invoke();
            Destroy(this);
        }
    }
}
