using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Cattas
{
    // SILK LAB - the first taste of the 2.5D world.
    //
    // Kittens don't live on a flat strip here. They walk a closed LOOP round the top dais tier of the Doll House picture:
    // along the front lip, round the vanity end, BEHIND the bath, behind the planter, and back to where they started.
    //
    // How it works (the "back end"):
    //  * The loop is a smooth closed curve through control points traced from the picture (Catmull-Rom spline).
    //  * Kittens are positioned by WORLD distance along the loop, not by pixels. World distance is measured with perspective:
    //    things further away (higher on the screen) are smaller, and sideways-into-the-screen movement is squashed
    //    (depth_squash) - so a kitten rounding the corner takes the time it really would, and speed looks right everywhere.
    //  * Position is evaluated analytically every frame (no pixel stepping), so movement is smooth at any refresh rate / 4K.
    //  * Draw order: the picture, then kittens that are BEHIND (z=1), then the bath/planter re-drawn from the picture, then kittens in front.
    public class SilkLab : MonoBehaviour
    {
        const int OrderBackground = -10, OrderBehind = 4, OrderOccluder = 6, OrderFront = 8;

        Camera cam;
        CatRig rig;
        System.Action onExit;

        float picW, picH, kittenScale = 3f, baseSpeed = 70f, squash = 0.3f, spawnGap = 1.3f;
        int count = 10;
        Vector2 planter = new Vector2(505f, 520f);
        readonly List<Vector3> controls = new List<Vector3>();      // x, y (picture px, y down), z (0 front .. 1 behind)

        // Dense samples of the loop
        Vector3[] sample;        // x, y, z
        float[] worldDist;       // cumulative world distance at each sample
        float totalWorld;

        Transform root;
        LineRenderer pathLine;
        readonly List<Texture2D> textures = new List<Texture2D>();
        readonly List<Walker> walkers = new List<Walker>();
        float spawnTimer;
        int spawned;
        float timeScale = 1f;
        bool paused, showPath;

        class Walker
        {
            public Transform wrapper;
            public CatPuppet puppet;
            public float w;          // world distance along the loop
            public float speedMul = 1f;
            public bool flip;
            public float fallY, fallVel;   // dropping out of the planter
            public bool falling;
            public float appear;
        }

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

        string pictureName = "level3_bg", occluderName = "level3_occl";

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
                    case "depth_squash": squash = Mathf.Max(0.05f, F(p[1])); break;
                    case "count": count = int.Parse(p[1], CultureInfo.InvariantCulture); break;
                    case "spawn_gap": spawnGap = F(p[1]); break;
                    case "planter": planter = new Vector2(F(p[1]), F(p[2])); break;
                    case "p": controls.Add(new Vector3(F(p[1]), F(p[2]), F(p[3]))); break;
                }
            }
            return controls.Count >= 4 && picW > 0 && picH > 0;
        }

        // ---------- the loop ----------
        // perspective size of a kitten at screen height y (further up the screen = further away = smaller)
        static float Scale(float y) => Mathf.Lerp(0.9f, 1.08f, Mathf.InverseLerp(585f, 650f, y));

        static Vector3 CatmullRom(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * b) + (-a + c) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (-a + 3f * b - 3f * c + d) * t3);
        }

        void BuildLoop()
        {
            const int perSegment = 48;
            int n = controls.Count;
            var pts = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                var a = controls[(i - 1 + n) % n]; var b = controls[i]; var c = controls[(i + 1) % n]; var d = controls[(i + 2) % n];
                for (int k = 0; k < perSegment; k++) pts.Add(CatmullRom(a, b, c, d, k / (float)perSegment));
            }
            sample = pts.ToArray();
            worldDist = new float[sample.Length + 1];
            for (int i = 0; i < sample.Length; i++)
            {
                var p = sample[i]; var q = sample[(i + 1) % sample.Length];
                float s = Scale((p.y + q.y) * 0.5f);
                float dx = (q.x - p.x) / s, dy = (q.y - p.y) / (s * squash);      // sideways is true length; into-the-screen is squashed
                worldDist[i + 1] = worldDist[i] + Mathf.Sqrt(dx * dx + dy * dy);
            }
            totalWorld = worldDist[sample.Length];
        }

        // position (picture px), z and direction at world distance w
        void At(float w, out Vector3 pos, out Vector2 dir)
        {
            w = Mathf.Repeat(w, totalWorld);
            int lo = 0, hi = sample.Length;                  // find the last sample with worldDist <= w
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (worldDist[mid] <= w) lo = mid; else hi = mid; }
            int next = (lo + 1) % sample.Length;
            float span = worldDist[lo + 1] - worldDist[lo];
            float t = span > 1e-5f ? (w - worldDist[lo]) / span : 0f;
            pos = Vector3.Lerp(sample[lo], sample[next], t);
            dir = new Vector2(sample[next].x - sample[lo].x, sample[next].y - sample[lo].y);
        }

        // ---------- scene ----------
        void BuildScene()
        {
            BuildLoop();
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

            var lineObj = new GameObject("Loop line");
            lineObj.transform.SetParent(root, false);
            pathLine = lineObj.AddComponent<LineRenderer>();
            pathLine.material = new Material(Shader.Find("Sprites/Default"));
            pathLine.startColor = pathLine.endColor = new Color(1f, 1f, 0.2f, 0.9f);
            pathLine.widthMultiplier = 2f;
            pathLine.loop = true;
            pathLine.sortingOrder = 20;
            var lp = new List<Vector3>();
            for (int i = 0; i < sample.Length; i += 4) lp.Add(new Vector3(sample[i].x, picH - sample[i].y, 0f));
            pathLine.positionCount = lp.Count;
            pathLine.SetPositions(lp.ToArray());
            pathLine.enabled = false;

            // fit the whole picture on screen
            float aspect = Mathf.Max(0.5f, Screen.width / (float)Screen.height);
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(picH / 2f, picW / 2f / aspect);
            cam.transform.position = new Vector3(picW / 2f, picH / 2f, -10f);
            cam.backgroundColor = new Color(0.1f, 0.07f, 0.06f);
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
            var wk = new Walker { wrapper = wrapper, puppet = puppet, falling = true, fallY = planter.y, fallVel = 0f };
            wk.speedMul = 1f + ((spawned * 37) % 11 - 5) * 0.012f;       // each kitten has a very slightly different pace
            // the loop starts at the planter: find the world distance of the first control point (sample 0)
            wk.w = 0f;
            walkers.Add(wk);
            spawned++;
            Place(wk, 0f);
        }

        void Place(Walker k, float dt)
        {
            Vector3 pos; Vector2 dir;
            if (k.falling)
            {
                // drop out of the planter's mouth onto the back of the tier (hidden behind the planter wall)
                k.fallVel += 900f * dt;
                k.fallY += k.fallVel * dt;
                At(0f, out pos, out dir);
                if (k.fallY >= pos.y) { k.falling = false; k.puppet.state = CatState.Walking; }
                else
                {
                    var p = new Vector3(pos.x, k.fallY, 1f);
                    k.puppet.state = CatState.Falling;
                    Apply(k, p, 1f, Scale(p.y), false);
                    return;
                }
            }
            At(k.w, out pos, out dir);
            // face the way we're walking; near the corners (mostly into/out of the screen) keep the last facing
            if (Mathf.Abs(dir.x) > 0.25f * dir.magnitude) k.flip = dir.x < 0f;
            Apply(k, pos, Mathf.Clamp01(pos.z), Scale(pos.y), k.flip);
        }

        void Apply(Walker k, Vector3 pos, float z, float scale, bool flip)
        {
            k.appear = Mathf.Min(1f, k.appear + 0.1f);
            k.wrapper.position = new Vector3(pos.x, picH - pos.y, 0f);
            k.wrapper.localScale = Vector3.one * (kittenScale * scale);
            k.puppet.flip = flip;
            k.puppet.SetOrder(z > 0.5f ? OrderBehind : OrderFront);
            float shade = Mathf.Lerp(1f, 0.86f, z);                // a touch darker when further back
            k.puppet.SetColor(new Color(shade, shade, shade, 1f));
        }

        void Update()
        {
            if (cam == null) return;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { Exit(); return; }
#else
            if (Input.GetKeyDown(KeyCode.Escape)) { Exit(); return; }
#endif
            if (paused) return;
            float dt = Time.deltaTime * timeScale;
            spawnTimer += dt;
            if (spawned < count && spawnTimer >= spawnGap) { spawnTimer = 0f; Spawn(); }
            foreach (var k in walkers)
            {
                if (!k.falling) k.w += baseSpeed * k.speedMul * dt;
                Place(k, dt);
            }
            if (pathLine != null) pathLine.enabled = showPath;
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
            float bh = buttonStyle.fontSize * 2.1f, bw = buttonStyle.fontSize * 9f, m = buttonStyle.fontSize * 0.6f;
            float x = m, y = m;
            GUI.Label(new Rect(x, y, bw * 3f, bh), "Silk Lab - Doll House loop (Esc = menu)", labelStyle); y += bh;
            if (GUI.Button(new Rect(x, y, bw, bh), "Back to menu", buttonStyle)) { Exit(); return; } y += bh + m * 0.5f;
            if (GUI.Button(new Rect(x, y, bw, bh), paused ? "Resume" : "Pause", buttonStyle)) paused = !paused; y += bh + m * 0.5f;
            if (GUI.Button(new Rect(x, y, bw, bh), showPath ? "Hide loop line" : "Show loop line", buttonStyle)) showPath = !showPath; y += bh + m * 0.5f;
            GUI.Label(new Rect(x, y, bw * 1.5f, bh), $"Speed x{timeScale:0.00}", labelStyle); y += bh * 0.8f;
            timeScale = GUI.HorizontalSlider(new Rect(x, y, bw * 1.4f, bh * 0.5f), timeScale, 0.1f, 3f); y += bh * 0.8f;
            if (GUI.Button(new Rect(x, y, bw, bh), "Restart kittens", buttonStyle)) Restart();
        }

        void Restart()
        {
            foreach (var k in walkers) if (k.wrapper != null) Destroy(k.wrapper.gameObject);
            walkers.Clear();
            spawned = 0;
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
