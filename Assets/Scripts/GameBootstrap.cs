using System.Collections.Generic;
using UnityEngine;

namespace Cattas
{
    // The "brain" of the game. It starts itself automatically when you press Play,
    // so there is nothing to set up in the Unity editor.
    //
    // Flow: Menu (level select) -> Playing a level -> Results -> back to Menu or next level.
    public class GameBootstrap : MonoBehaviour
    {
        public const int TicksPerSecond = 30; // game logic runs at a steady 30 steps a second

        enum GameMode { Menu, Playing, Results }
        GameMode mode = GameMode.Menu;

        Camera cam;
        Transform levelRoot;
        DiggableGround ground;
        Sprite catSprite;
        readonly List<Cat> cats = new List<Cat>();
        readonly List<Vector2Int> spawnPoints = new List<Vector2Int>();
        readonly List<RectInt> exitZones = new List<RectInt>();

        // Current level
        int levelNumber;
        int levelCount;
        LevelData level;
        int levelWidth = 640, levelHeight = 360;
        readonly int[] traitsLeft = new int[CatTraits.Count];
        CatTrait selectedTrait = CatTrait.LitterLord;

        int released, saved, lost, ticksUntilNextCat, ticksLeft;
        bool timeUp;

        // Game speed in %: 50 = normal, 100 = double speed.
        // Players can only speed up from the level's starting speed (slower than normal will be an NG+ unlock).
        float speedPercent = 50f;
        int minSpeedPercent = 50;
        float tickTimer;

        // Saved progress (stays between play sessions)
        const string UnlockedKey = "cattas_unlocked";
        static string BestKey(int n) => $"cattas_best_{n}";

        static readonly Color[] CatColours =
        {
            new Color(1f, 0.62f, 0.25f),    // ginger
            new Color(0.62f, 0.62f, 0.68f), // grey
            new Color(1f, 0.93f, 0.78f),    // cream
        };

        static readonly string[] CatArt =
        {
            // w = fur, k = eye, p = nose, . = see-through. Facing right.
            "......w...w",
            "......ww.ww",
            "......wwwww",
            "......wkwkw",
            "w.....wwpww",
            "w.wwwwwwwww",
            ".wwwwwwwww.",
            "..wwwwwwww.",
            "..w.w..w.w.",
            "..w.w..w.w.",
        };

        static Material spriteMaterial;
        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    spriteMaterial = new Material(shader);
                }
                return spriteMaterial;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (FindAnyObjectByType<GameBootstrap>() == null)
                new GameObject("Cattas Game").AddComponent<GameBootstrap>();
        }

        void Start()
        {
            SetupCamera();
            catSprite = SpriteMaker.FromArt(CatArt, new Vector2(0.5f, 0f));
            levelCount = LevelData.CountLevels();
            mode = GameMode.Menu;
        }

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var camObj = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camObj.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.76f, 0.95f); // sky blue
            cam.transform.position = new Vector3(320f, 180f, -10f);
            cam.orthographicSize = 180f;
        }

        // ---------- Progress ----------
        int Unlocked => Mathf.Max(1, PlayerPrefs.GetInt(UnlockedKey, 1));

        void RecordResult()
        {
            if (saved > PlayerPrefs.GetInt(BestKey(levelNumber), -1))
                PlayerPrefs.SetInt(BestKey(levelNumber), saved);
            if (saved >= level.need && levelNumber + 1 > Unlocked)
                PlayerPrefs.SetInt(UnlockedKey, levelNumber + 1);
            PlayerPrefs.Save();
        }

        // ---------- Building a level from its file ----------
        void StartLevel(int number)
        {
            var data = LevelData.Load(number);
            if (data == null || data.Rows == 0 || data.Cols == 0)
            {
                Debug.LogError($"Cattas: couldn't load level {number} (Assets/Resources/Levels/level{number:00}.txt)");
                mode = GameMode.Menu;
                return;
            }
            levelNumber = number;
            level = data;
            BuildLevel();
            mode = GameMode.Playing;
        }

        void ClearLevel()
        {
            if (levelRoot != null) Destroy(levelRoot.gameObject);
            levelRoot = null;
            cats.Clear();
        }

        void BuildLevel()
        {
            ClearLevel();
            spawnPoints.Clear();
            exitZones.Clear();
            released = saved = lost = 0;
            timeUp = false;
            ticksUntilNextCat = TicksPerSecond; // first cat after 1 second
            ticksLeft = level.timeSeconds * TicksPerSecond;
            for (int i = 0; i < traitsLeft.Length; i++) traitsLeft[i] = level.traits[i];
            selectedTrait = FirstAvailableTrait();
            minSpeedPercent = level.speed;
            speedPercent = level.speed;

            levelWidth = level.PixelWidth;
            levelHeight = level.PixelHeight;

            levelRoot = new GameObject("Level " + levelNumber).transform;
            levelRoot.SetParent(transform, false);

            var groundObj = new GameObject("Ground");
            groundObj.transform.SetParent(levelRoot, false);
            ground = groundObj.AddComponent<DiggableGround>();
            ground.Init(levelWidth, levelHeight);

            const int cs = LevelData.CellSize;
            for (int row = 0; row < level.Rows; row++)
            {
                int y0 = (level.Rows - 1 - row) * cs; // row 0 is the top of the map
                for (int col = 0; col < level.Cols; col++)
                {
                    int x0 = col * cs;
                    switch (level.Cell(col, row))
                    {
                        case '#': ground.FillRect(x0, y0, x0 + cs, y0 + cs); break;
                        case 'X': ground.FillHardRect(x0, y0, x0 + cs, y0 + cs); break;
                        case '/': ground.FillRamp(x0, y0, cs, true); break;
                        case '\\': ground.FillRamp(x0, y0, cs, false); break;
                        case 'H': AddHatch(x0 + cs / 2, y0); break;
                        case 'E': AddExit(x0 + cs / 2, y0); break;
                    }
                }
            }
            ground.FinishPainting();

            if (spawnPoints.Count == 0) AddHatch(levelWidth / 4, levelHeight - 40);

            SetupCameraForLevel();
        }

        void AddHatch(int centreX, int bottomY)
        {
            spawnPoints.Add(new Vector2Int(centreX, bottomY - 2));
            MakeBox("Hatch", new Vector2Int(centreX - 12, bottomY), 24, 10, new Color32(70, 60, 60, 255), 1);
        }

        void AddExit(int centreX, int floorY)
        {
            MakeBox("Exit House", new Vector2Int(centreX - 10, floorY), 20, 26, new Color32(150, 90, 50, 255), 1);
            MakeBox("Exit Door", new Vector2Int(centreX - 5, floorY), 10, 14, new Color32(40, 25, 20, 255), 2);
            exitZones.Add(new RectInt(centreX - 4, floorY, 9, 10));
        }

        void MakeBox(string name, Vector2Int pos, int w, int h, Color32 colour, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(levelRoot, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteMaker.Solid(w, h, colour, Vector2.zero);
            sr.material = SpriteMaterial;
            sr.sortingOrder = order;
        }

        CatTrait FirstAvailableTrait()
        {
            for (int i = 0; i < traitsLeft.Length; i++)
                if (traitsLeft[i] > 0) return (CatTrait)i;
            return CatTrait.LitterLord;
        }

        // ---------- Camera ----------
        // The whole level height fits on screen, with a little room at the bottom for the trait bar.
        // Wider levels scroll left/right.
        void SetupCameraForLevel()
        {
            float viewHeight = levelHeight * 1.12f;
            cam.orthographicSize = viewHeight / 2f;
            float camY = levelHeight * 1.02f - viewHeight / 2f;
            float startX = spawnPoints.Count > 0 ? spawnPoints[0].x : levelWidth / 2f;
            cam.transform.position = new Vector3(startX, camY, -10f);
            ClampCamera();
        }

        void ClampCamera()
        {
            float halfW = cam.orthographicSize * cam.aspect;
            var p = cam.transform.position;
            if (levelWidth <= halfW * 2f) p.x = levelWidth / 2f;
            else p.x = Mathf.Clamp(p.x, halfW, levelWidth - halfW);
            cam.transform.position = p;
        }

        float KeyboardScroll()
        {
            float v = 0f;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return 0f;
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) v -= 1f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) v += 1f;
#else
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) v -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) v += 1f;
#endif
            return v;
        }

        // ---------- Game loop ----------
        void Update()
        {
            if (mode != GameMode.Playing) return;

            float scroll = KeyboardScroll();
            if (scroll != 0f)
            {
                cam.transform.position += Vector3.right * scroll * cam.orthographicSize * 1.5f * Time.unscaledDeltaTime;
                ClampCamera();
            }

            tickTimer += Time.deltaTime * (speedPercent / 50f);
            float tickLength = 1f / TicksPerSecond;
            int safety = 0;
            while (tickTimer >= tickLength && safety++ < 20 && mode == GameMode.Playing)
            {
                tickTimer -= tickLength;
                GameTick();
            }
            if (safety >= 20) tickTimer = 0f;
        }

        void GameTick()
        {
            if (released < level.cats && --ticksUntilNextCat <= 0)
            {
                SpawnCat();
                ticksUntilNextCat = Mathf.Max(1, Mathf.RoundToInt(level.rate * TicksPerSecond));
            }
            for (int i = cats.Count - 1; i >= 0; i--)
                cats[i].Tick();

            if (--ticksLeft <= 0) timeUp = true;

            if (IsFinished)
            {
                RecordResult();
                mode = GameMode.Results;
            }
        }

        void SpawnCat()
        {
            var spawn = spawnPoints[released % spawnPoints.Count];
            var go = new GameObject("Cat " + (released + 1));
            go.transform.SetParent(levelRoot, false);
            var cat = go.AddComponent<Cat>();
            cat.Init(this, ground, spawn.x, spawn.y, catSprite, CatColours[released % CatColours.Length]);
            cats.Add(cat);
            released++;
        }

        public bool IsAtExit(int x, int y)
        {
            foreach (var zone in exitZones)
                if (zone.Contains(new Vector2Int(x, y))) return true;
            return false;
        }

        public void OnCatSaved(Cat cat) { cats.Remove(cat); saved++; }
        public void OnCatLost(Cat cat) { cats.Remove(cat); lost++; }

        // The level is over when time runs out, or every cat is out and the only ones left
        // are Biscuit Bakers (they're busy and will never leave).
        bool IsFinished
        {
            get
            {
                if (timeUp) return true;
                if (released < level.cats) return false;
                foreach (var cat in cats)
                    if (cat.State != CatState.Blocking) return false;
                return true;
            }
        }

        // ZAP! A bright flash, and a frizzy cat that shoots off home to recover.
        public void SpawnZap(int x, int y, Sprite sprite, Color tint)
        {
            var flash = new GameObject("Zap Flash");
            flash.transform.SetParent(levelRoot, false);
            flash.transform.localPosition = new Vector3(x + 0.5f, y + 5f, 0f);
            var fsr = flash.AddComponent<SpriteRenderer>();
            fsr.sprite = SpriteMaker.Circle(14, new Color32(255, 240, 120, 255));
            fsr.material = SpriteMaterial;
            fsr.sortingOrder = 10;
            var ffade = flash.AddComponent<FadeAway>();
            ffade.life = 0.3f;
            ffade.grow = 0.6f;

            var frizz = new GameObject("Frizzy Cat");
            frizz.transform.SetParent(levelRoot, false);
            frizz.transform.localPosition = new Vector3(x + 0.5f, y, 0f);
            frizz.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            var csr = frizz.AddComponent<SpriteRenderer>();
            csr.sprite = sprite;
            csr.color = tint * 0.45f + new Color(0f, 0f, 0f, 0.55f);
            csr.material = SpriteMaterial;
            csr.sortingOrder = 11;
            var cfade = frizz.AddComponent<FadeAway>();
            cfade.life = 1.2f;
            cfade.rise = 60f;
        }

        // Is there a Biscuit Baker just in front of this walking cat?
        public bool IsBlockedByBaker(Cat walker, int nextX, int y)
        {
            foreach (var cat in cats)
            {
                if (cat == walker || cat.State != CatState.Blocking) continue;
                if (Mathf.Abs(cat.Y - y) > 8) continue;
                bool inFront = nextX > walker.X ? cat.X > walker.X : cat.X < walker.X;
                if (inFront && Mathf.Abs(cat.X - nextX) <= 4) return true;
            }
            return false;
        }

        // ---------- Picking cats ----------
        Cat CatUnderMouse(Vector2 guiMousePos)
        {
            var screen = new Vector3(guiMousePos.x, Screen.height - guiMousePos.y, 0f);
            Vector3 world = cam.ScreenToWorldPoint(screen);
            Cat best = null;
            float bestDist = float.MaxValue;
            foreach (var cat in cats)
            {
                float dx = Mathf.Abs(world.x - (cat.X + 0.5f));
                float dy = world.y - cat.Y;
                if (dx > 7f || dy < -3f || dy > 14f) continue;
                float d = dx + Mathf.Abs(dy - 5f);
                if (d < bestDist) { bestDist = d; best = cat; }
            }
            return best;
        }

        void TryAssign(Cat cat)
        {
            if (cat == null || traitsLeft[(int)selectedTrait] <= 0) return;
            if (cat.TryGiveTrait(selectedTrait))
                traitsLeft[(int)selectedTrait]--;
        }

        void DrawOutline(Cat cat, Color colour)
        {
            Vector3 a = cam.WorldToScreenPoint(new Vector3(cat.X - 6f, cat.Y - 2f, 0f));
            Vector3 b = cam.WorldToScreenPoint(new Vector3(cat.X + 7f, cat.Y + 12f, 0f));
            var r = Rect.MinMaxRect(a.x, Screen.height - b.y, b.x, Screen.height - a.y);
            float t = Mathf.Max(2f, Screen.height / 400f);
            var old = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMin, r.yMax - t, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMin, r.yMin, t, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.yMin, t, r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        // ---------- On-screen text and buttons ----------
        int fs;
        GUIStyle label, small, button, big;

        void MakeStyles()
        {
            fs = Mathf.Max(14, Screen.height / 36);
            label = new GUIStyle(GUI.skin.label) { fontSize = fs, fontStyle = FontStyle.Bold };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = Mathf.RoundToInt(fs * 0.8f), fontStyle = FontStyle.Normal, wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { fontSize = fs, wordWrap = true };
            big = new GUIStyle(label) { fontSize = fs * 2, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        }

        void OnGUI()
        {
            MakeStyles();
            switch (mode)
            {
                case GameMode.Menu: DrawMenu(); break;
                case GameMode.Playing: DrawPlaying(); break;
                case GameMode.Results: DrawPlaying(); DrawResults(); break;
            }
        }

        void DrawMenu()
        {
            var title = new GUIStyle(big) { fontSize = fs * 4 };
            GUI.Label(new Rect(0, Screen.height * 0.05f, Screen.width, fs * 5), "CATTAS", title);
            GUI.Label(new Rect(0, Screen.height * 0.05f + fs * 4.5f, Screen.width, fs * 2),
                "Get the cats home!", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });

            if (levelCount == 0)
            {
                GUI.Label(new Rect(0, Screen.height * 0.5f, Screen.width, fs * 3),
                    "No levels found in Assets/Resources/Levels", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
                return;
            }

            const int perRow = 5;
            float bw = fs * 9f, bh = fs * 4f, gap = fs * 0.6f;
            float gridW = perRow * bw + (perRow - 1) * gap;
            float x0 = (Screen.width - gridW) / 2f;
            float y0 = Screen.height * 0.32f;
            int unlocked = Unlocked;
            var oldEnabled = GUI.enabled;
            for (int i = 1; i <= levelCount; i++)
            {
                int col = (i - 1) % perRow, row = (i - 1) / perRow;
                var r = new Rect(x0 + col * (bw + gap), y0 + row * (bh + gap), bw, bh);
                bool open = i <= unlocked;
                int best = PlayerPrefs.GetInt(BestKey(i), -1);
                string text = open ? (best >= 0 ? $"Level {i}\nBest: {best} saved" : $"Level {i}") : $"Level {i}\n(locked)";
                GUI.enabled = open;
                if (GUI.Button(r, text, button)) StartLevel(i);
            }
            GUI.enabled = oldEnabled;

            // Little helpers for testing
            float hb = fs * 9f;
            if (GUI.Button(new Rect(Screen.width - hb - fs, Screen.height - fs * 2.5f, hb, fs * 1.8f), "Unlock all (test)", small))
            {
                PlayerPrefs.SetInt(UnlockedKey, levelCount);
                PlayerPrefs.Save();
            }
            if (GUI.Button(new Rect(Screen.width - hb * 2 - fs * 1.5f, Screen.height - fs * 2.5f, hb, fs * 1.8f), "Reset progress", small))
            {
                PlayerPrefs.DeleteKey(UnlockedKey);
                for (int i = 1; i <= levelCount; i++) PlayerPrefs.DeleteKey(BestKey(i));
                PlayerPrefs.Save();
            }
        }

        void DrawPlaying()
        {
            float pad = fs * 0.6f, lineH = fs * 1.6f;

            // Top-left info panel
            float panelW = fs * 30;
            GUI.Box(new Rect(pad, pad, panelW, lineH * 3.4f), GUIContent.none);
            int secs = Mathf.Max(0, Mathf.CeilToInt(ticksLeft / (float)TicksPerSecond));
            GUI.Label(new Rect(pad * 2, pad, panelW, lineH), $"Level {levelNumber}: {level.name}", label);
            GUI.Label(new Rect(pad * 2, pad + lineH, panelW, lineH),
                $"Out {released}/{level.cats}   Home {saved} (need {level.need})   Lost {lost}   Time {secs / 60}:{secs % 60:00}", small);
            GUI.Label(new Rect(pad * 2, pad + lineH * 2f, panelW - pad * 2, lineH * 1.4f), level.hint, small);

            // Top-right buttons
            float bw = fs * 7;
            if (mode == GameMode.Playing)
            {
                // Speed slider
                float sw = fs * 10f, sx = Screen.width - pad - sw;
                GUI.Box(new Rect(sx - pad * 0.5f, pad * 0.5f, sw + pad, lineH * 1.9f), GUIContent.none);
                GUI.Label(new Rect(sx, pad * 0.5f, sw, lineH), $"Speed {Mathf.RoundToInt(speedPercent)}%",
                    new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
                speedPercent = Mathf.Round(GUI.HorizontalSlider(new Rect(sx, pad * 0.5f + lineH, sw, lineH * 0.7f),
                    speedPercent, minSpeedPercent, 100f));

                float by = pad + lineH * 2f;
                if (GUI.Button(new Rect(Screen.width - pad - bw, by, bw, lineH), "Restart", button))
                    BuildLevel();
                if (GUI.Button(new Rect(Screen.width - pad - bw, by + lineH * 1.2f, bw, lineH), "Levels", button))
                {
                    ClearLevel();
                    mode = GameMode.Menu;
                    return;
                }
            }

            if (levelWidth > cam.orthographicSize * cam.aspect * 2f)
                GUI.Label(new Rect(0, pad, Screen.width, lineH), "Scroll: arrow keys / A D, or right-click drag",
                    new GUIStyle(small) { alignment = TextAnchor.UpperCenter });

            if (mode != GameMode.Playing) return;

            DrawTraitBar(pad);

            // Cable Chewer countdowns above cats' heads
            var fuseStyle = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(fs * 1.1f) };
            fuseStyle.normal.textColor = new Color(1f, 0.9f, 0.2f);
            foreach (var cat in cats)
            {
                int fuse = cat.FuseSecondsLeft;
                if (fuse <= 0) continue;
                Vector3 p = cam.WorldToScreenPoint(new Vector3(cat.X + 0.5f, cat.Y + 18f, 0f));
                GUI.Label(new Rect(p.x - fs, Screen.height - p.y - fs, fs * 2, fs * 2), fuse.ToString(), fuseStyle);
            }

            Event e = Event.current;

            // Right- or middle-drag to scroll wide levels
            if (e.type == EventType.MouseDrag && (e.button == 1 || e.button == 2))
            {
                float worldPerPixel = cam.orthographicSize * 2f / Screen.height;
                cam.transform.position -= Vector3.right * e.delta.x * worldPerPixel;
                ClampCamera();
                e.Use();
                return;
            }

            // Highlight the cat under the mouse, and give it the trait on click.
            Cat hovered = CatUnderMouse(e.mousePosition);
            if (hovered != null) DrawOutline(hovered, Color.white);
            if (e.type == EventType.MouseDown && e.button == 0 && hovered != null)
            {
                TryAssign(hovered);
                e.Use();
            }
        }

        void DrawTraitBar(float pad)
        {
            float bw = fs * 6.2f, bh = fs * 3.2f, gap = fs * 0.3f;
            float total = CatTraits.Count * bw + (CatTraits.Count - 1) * gap;
            float x0 = (Screen.width - total) / 2f;
            float y0 = Screen.height - pad - bh;
            var style = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(fs * 0.8f), wordWrap = true };
            var oldBg = GUI.backgroundColor;
            var oldEnabled = GUI.enabled;

            for (int i = 0; i < CatTraits.Count; i++)
            {
                var trait = (CatTrait)i;
                bool usable = CatTraits.IsImplemented(trait) && traitsLeft[i] > 0;
                GUI.enabled = usable;
                GUI.backgroundColor = (usable && trait == selectedTrait) ? new Color(1f, 0.8f, 0.3f) : oldBg;
                if (GUI.Button(new Rect(x0 + i * (bw + gap), y0, bw, bh), $"{CatTraits.DisplayName(trait)}\n{traitsLeft[i]}", style))
                    selectedTrait = trait;
            }
            GUI.enabled = oldEnabled;
            GUI.backgroundColor = oldBg;
        }

        void DrawResults()
        {
            bool won = saved >= level.need;
            var box = new Rect(Screen.width * 0.2f, Screen.height * 0.28f, Screen.width * 0.6f, Screen.height * 0.44f);
            GUI.Box(box, GUIContent.none);
            GUI.Box(box, GUIContent.none);

            string msg;
            if (won)
                msg = saved == level.cats ? $"PURR-FECT!\nAll {saved} cats made it home!" : $"Level complete!\n{saved} cats home (needed {level.need})";
            else
                msg = (timeUp ? "Out of time!\n" : "") + $"Only {saved} cats made it home...\nYou need {level.need}.";
            GUI.Label(new Rect(box.x, box.y, box.width, box.height * 0.65f), msg, big);

            float bw = fs * 8f, bh = fs * 2f, by = box.yMax - bh * 1.5f;
            bool hasNext = won && levelNumber < levelCount;
            int buttons = hasNext ? 3 : 2;
            float total = buttons * bw + (buttons - 1) * fs;
            float bx = box.center.x - total / 2f;

            if (hasNext)
            {
                if (GUI.Button(new Rect(bx, by, bw, bh), "Next level", button)) { StartLevel(levelNumber + 1); return; }
                bx += bw + fs;
            }
            if (GUI.Button(new Rect(bx, by, bw, bh), won ? "Play again" : "Try again", button)) { BuildLevel(); mode = GameMode.Playing; return; }
            bx += bw + fs;
            if (GUI.Button(new Rect(bx, by, bw, bh), "Levels", button)) { ClearLevel(); mode = GameMode.Menu; }
        }
    }
}
