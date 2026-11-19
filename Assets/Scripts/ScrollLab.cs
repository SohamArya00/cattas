using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Cattas
{
    // SCROLL LAB - three screens joined into one long level you scroll through left to right.
    //
    //   screen 1  LEFT    the wall drawn at about 45 degrees (big at the far left, shrinking towards the corner)
    //   screen 2  MIDDLE  flat, head-on
    //   screen 3  RIGHT   mirror of screen 1 (grows from the corner towards the far right)
    //
    // Nothing is folded or turned into 3D: each screen is just a picture. The 2.5D is in the numbers along the walking route:
    // every point of the route knows how big a kitten should be there (scale) and how squashed sideways the wall is (f),
    // so a kitten walking towards the corner shrinks and slows on screen, and grows and speeds up walking away from it.
    // Kittens are placed by WORLD distance along the route (screen distance / (scale * squash)), evaluated every frame.
    public class ScrollLab : MonoBehaviour
    {
        Camera cam;
        CatRig rig;
        System.Action onExit;

        float screenW = 1920f, screenH = 1080f, kittenRigScale = 3.4f, baseSpeed = 95f, spawnGap = 1.5f;
        int count = 8;
        readonly List<string> screenNames = new List<string>();
        readonly List<Vector4> controls = new List<Vector4>();       // x, y (px, y down), scale, squash
        Vector4[] sample; float[] dist; float total;

        Transform root;
        LineRenderer routeLine;
        readonly List<Texture2D> textures = new List<Texture2D>();
        readonly List<Walker> walkers = new List<Walker>();
        float spawnTimer; int spawned;
        float camX, targetX;
        bool paused, showRoute, follow;
        int followIndex;
        float timeScale = 1f;
        Rect panelRect;
        bool dragging; Vector2 lastMouse;

        class Walker { public Transform wrapper; public CatPuppet puppet; public float w, speed; public int dir = 1; public bool flip; }

        float StripW => screenW * screenNames.Count;

        public void Init(Camera camera, CatRig kittenRig, System.Action exit)
        {
            cam = camera; rig = kittenRig; onExit = exit;
            if (!Load("Levels/scroll_test")) { Debug.LogError("Cattas: Scroll Lab data missing"); Exit(); return; }
            Build();
        }

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
                    case "screens": screenW = F(p[1]); screenH = F(p[2]); for (int i = 3; i < p.Length; i++) screenNames.Add(p[i]); break;
                    case "kitten": kittenRigScale = F(p[1]); break;
                    case "speed": baseSpeed = F(p[1]); break;
                    case "count": count = int.Parse(p[1], CultureInfo.InvariantCulture); break;
                    case "spawn_gap": spawnGap = F(p[1]); break;
                    case "r": controls.Add(new Vector4(F(p[1]), F(p[2]), F(p[3]), F(p[4]))); break;
                }
            }
            return screenNames.Count > 0 && controls.Count >= 2;
        }

        static Vector4 CatmullRom(Vector4 a, Vector4 b, Vector4 c, Vector4 d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * b) + (-a + c) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (-a + 3f * b - 3f * c + d) * t3);
        }

        void BuildRoute()
        {
            const int perSeg = 8;
            int n = controls.Count;
            var pts = new List<Vector4>();
            for (int i = 0; i < n - 1; i++)
            {
                var a = controls[Mathf.Max(0, i - 1)]; var b = controls[i]; var c = controls[i + 1]; var d = controls[Mathf.Min(n - 1, i + 2)];
                for (int k = 0; k < perSeg; k++) pts.Add(CatmullRom(a, b, c, d, k / (float)perSeg));
            }
            pts.Add(controls[n - 1]);
            sample = pts.ToArray();
            dist = new float[sample.Length];
            for (int i = 1; i < sample.Length; i++)
            {
                var p = sample[i - 1]; var q = sample[i];
                float s = Mathf.Max(0.2f, (p.z + q.z) * 0.5f), f = Mathf.Max(0.2f, (p.w + q.w) * 0.5f);
                float dx = (q.x - p.x) / (s * f), dy = (q.y - p.y) / (s * 0.3f);
                dist[i] = dist[i - 1] + Mathf.Sqrt(dx * dx + dy * dy);
            }
            total = dist[sample.Length - 1];
        }

        Vector4 At(float w)
        {
            w = Mathf.Clamp(w, 0f, total);
            int lo = 0, hi = sample.Length - 1;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (dist[mid] <= w) lo = mid; else hi = mid; }
            float span = dist[hi] - dist[lo];
            float t = span > 1e-5f ? (w - dist[lo]) / span : 0f;
            return Vector4.Lerp(sample[lo], sample[hi], t);
        }

        void Build()
        {
            BuildRoute();
            root = new GameObject("Scroll Lab").transform;
            root.SetParent(transform, false);
            for (int i = 0; i < screenNames.Count; i++)
            {
                var tex = Art.Load(screenNames[i]);
                if (tex == null) continue;
                textures.Add(tex);
                var go = new GameObject("Screen " + (i + 1));
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(i * screenW, 0f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, tex.width / screenW);
                sr.material = GameBootstrap.SpriteMaterial;
                sr.sortingOrder = -10;
            }
            var lineObj = new GameObject("Route line");
            lineObj.transform.SetParent(root, false);
            routeLine = lineObj.AddComponent<LineRenderer>();
            routeLine.material = new Material(Shader.Find("Sprites/Default"));
            routeLine.startColor = routeLine.endColor = new Color(1f, 1f, 0.2f, 0.9f);
            routeLine.widthMultiplier = 3f;
            routeLine.sortingOrder = 20;
            var lp = new List<Vector3>();
            for (int i = 0; i < sample.Length; i += 2) lp.Add(new Vector3(sample[i].x, screenH - sample[i].y, 0f));
            routeLine.positionCount = lp.Count;
            routeLine.SetPositions(lp.ToArray());
            routeLine.enabled = false;

            cam.orthographic = true;
            cam.orthographicSize = screenH / 2f;
            cam.backgroundColor = new Color(0.1f, 0.07f, 0.06f);
            camX = targetX = HalfView();
            ApplyCam();
        }

        float HalfView() => screenH * 0.5f * Mathf.Max(0.5f, Screen.width / (float)Screen.height);

        void ApplyCam()
        {
            float half = HalfView();
            float lo = half, hi = Mathf.Max(half, StripW - half);
            targetX = Mathf.Clamp(targetX, lo, hi);
            camX = Mathf.Clamp(camX, lo, hi);
            cam.orthographicSize = screenH / 2f;
            cam.transform.position = new Vector3(camX, screenH / 2f, -10f);
        }

        // ---------- kittens ----------
        void Spawn()
        {
            if (rig == null) return;
            var wrapper = new GameObject("Kitten").transform;
            wrapper.SetParent(root, false);
            var puppet = CatPuppet.Create(wrapper, rig, Color.white, 8);
            puppet.state = CatState.Walking;
            var k = new Walker { wrapper = wrapper, puppet = puppet, w = 0f, dir = 1, speed = baseSpeed * (1f + ((spawned * 37) % 11 - 5) * 0.012f) };
            walkers.Add(k);
            spawned++;
            Place(k, 0f);
        }

        void Place(Walker k, float prevW)
        {
            var p = At(k.w);
            float sc = p.z;
            k.wrapper.position = new Vector3(p.x, screenH - p.y, 0f);
            k.wrapper.localScale = Vector3.one * (kittenRigScale * sc);
            k.puppet.moveScale = 1f / (kittenRigScale * Mathf.Max(0.2f, sc));
            k.puppet.flip = k.dir < 0;
        }

        void Update()
        {
            if (cam == null || root == null) return;
            ReadInput();
            if (cam == null || root == null) return;
            float dt = Time.unscaledDeltaTime;
            float step = paused ? 0f : dt * timeScale;
            spawnTimer += step;
            if (spawned < count && spawnTimer >= spawnGap) { spawnTimer = 0f; Spawn(); }
            foreach (var k in walkers)
            {
                k.w += k.dir * k.speed * step;
                if (k.w <= 0f) { k.w = 0f; k.dir = 1; }
                else if (k.w >= total) { k.w = total; k.dir = -1; }
                Place(k, k.w);
            }
            if (follow && walkers.Count > 0)
            {
                followIndex = Mathf.Clamp(followIndex, 0, walkers.Count - 1);
                targetX = walkers[followIndex].wrapper.position.x;
            }
            camX = Mathf.Lerp(camX, targetX, 1f - Mathf.Exp(-5f * dt));            // smooth glide
            ApplyCam();
            if (routeLine != null) routeLine.enabled = showRoute;
        }

        void ReadInput()
        {
            bool escape = false, dragHeld = false, dragStart = false;
            Vector2 mouse = Vector2.zero; float wheel = 0f, key = 0f;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var ms = UnityEngine.InputSystem.Mouse.current;
            if (kb != null)
            {
                escape = kb.escapeKey.wasPressedThisFrame;
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) key -= 1f;
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) key += 1f;
            }
            if (ms != null)
            {
                mouse = ms.position.ReadValue(); wheel = ms.scroll.ReadValue().y / 120f;
                dragHeld = ms.rightButton.isPressed || ms.middleButton.isPressed;
                dragStart = ms.rightButton.wasPressedThisFrame || ms.middleButton.wasPressedThisFrame;
            }
#else
            escape = Input.GetKeyDown(KeyCode.Escape);
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) key -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) key += 1f;
            mouse = Input.mousePosition; wheel = Input.mouseScrollDelta.y;
            dragHeld = Input.GetMouseButton(1) || Input.GetMouseButton(2);
            dragStart = Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
#endif
            if (escape) { Exit(); return; }
            float dt = Time.unscaledDeltaTime;
            bool overPanel = panelRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
            float move = 0f;
            move += key * 1100f * dt;
            if (!overPanel) move -= wheel * 420f;
            // push the mouse to the left/right edge of the screen to scroll
            if (!overPanel && Application.isFocused && mouse.x >= 0 && mouse.x < Screen.width && mouse.y >= 0 && mouse.y < Screen.height)
            {
                float edge = Mathf.Max(8f, Screen.width * 0.015f);
                if (mouse.x > Screen.width - edge) move += 1100f * dt; else if (mouse.x < edge) move -= 1100f * dt;
            }
            if (dragStart && !overPanel) { dragging = true; lastMouse = mouse; }
            if (!dragHeld) dragging = false;
            if (dragging) { move -= (mouse.x - lastMouse.x) * (screenH / Screen.height); lastMouse = mouse; }
            if (!Mathf.Approximately(move, 0f)) { follow = false; targetX += move; }
        }

        // ---------- buttons + little map ----------
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
            GUI.Label(new Rect(x, y, bw * 3f, bh), "Scroll Lab - 3 screens (Esc = menu)", labelStyle); y += bh;
            if (GUI.Button(new Rect(x, y, bw, bh), "Back to menu", buttonStyle)) { Exit(); return; } y += bh + m * 0.5f;
            if (GUI.Button(new Rect(x, y, bw, bh), paused ? "Resume" : "Pause", buttonStyle)) paused = !paused; y += bh + m * 0.5f;
            if (GUI.Button(new Rect(x, y, bw, bh), showRoute ? "Hide route" : "Show route", buttonStyle)) showRoute = !showRoute; y += bh + m * 0.5f;
            if (GUI.Button(new Rect(x, y, bw * 1.3f, bh), follow ? "Following kitten " + (followIndex + 1) + " (next)" : "Follow a kitten", buttonStyle))
            {
                if (follow) followIndex = (followIndex + 1) % Mathf.Max(1, walkers.Count);
                follow = true;
            }
            y += bh + m * 0.5f;
            GUI.Label(new Rect(x, y, bw * 1.5f, bh), $"Speed x{timeScale:0.00}", labelStyle); y += bh * 0.8f;
            timeScale = GUI.HorizontalSlider(new Rect(x, y, bw * 1.4f, bh * 0.5f), timeScale, 0.1f, 3f); y += bh * 0.8f;
            GUI.Label(new Rect(x, y, bw * 2.6f, bh * 1.8f), "Arrows / A D / wheel / right-drag /\nscreen edge: scroll", labelStyle); y += bh * 1.8f;
            panelRect = new Rect(0, 0, bw * 1.6f + m * 2f, y + m);

            // the little map of all three screens along the bottom
            float mapW = Mathf.Min(Screen.width * 0.5f, 900f), mapH = mapW / (screenNames.Count * screenW / screenH);
            var map = new Rect((Screen.width - mapW) / 2f, Screen.height - mapH - m, mapW, mapH);
            float segW = mapW / screenNames.Count;
            for (int i = 0; i < textures.Count && i < screenNames.Count; i++)
                GUI.DrawTexture(new Rect(map.x + i * segW, map.y, segW, mapH), textures[i], ScaleMode.StretchToFill);
            float mapScale = mapW / StripW;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 0.3f, 1f);
            GUI.Box(new Rect(map.x + (camX - HalfView()) * mapScale, map.y, 2f * HalfView() * mapScale, mapH), GUIContent.none);
            GUI.color = Color.white;
            foreach (var k in walkers)
                GUI.DrawTexture(new Rect(map.x + k.wrapper.position.x * mapScale - 3f, map.y + (1f - k.wrapper.position.y / screenH) * mapH - 3f, 6f, 6f), Texture2D.whiteTexture);
            GUI.color = old;
            if (map.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                follow = false;
                targetX = (Event.current.mousePosition.x - map.x) / mapScale;
                Event.current.Use();
            }
            panelRect = Rect.MinMaxRect(0, 0, Mathf.Max(panelRect.xMax, 0), Screen.height);   // keep the whole left strip clear of scroll clicks
            panelRect = new Rect(0, 0, bw * 1.6f + m * 2f, y + m);
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
