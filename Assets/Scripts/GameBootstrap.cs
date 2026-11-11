using System.Collections.Generic;
using UnityEngine;

namespace Cattas
{
    public enum FxKind { Dig, Smash, Hairball, Zap, Splat, Puff, Home }

    // The "brain" of the game. It starts itself automatically when you press Play,
    // so there is nothing to set up in the Unity editor.
    //
    // Flow: Menu (level select) -> Playing a level -> Results -> back to Menu or next level.
    public class GameBootstrap : MonoBehaviour
    {
        public const int TicksPerSecond = 30; // game logic runs at a steady 30 steps a second

        // How far we are between one game step and the next (0..1) - used to make movement smooth.
        public static float TickAlpha { get; private set; }

        enum GameMode { Menu, Playing, Results }
        GameMode mode = GameMode.Menu;

        Camera cam;
        Transform levelRoot;
        DiggableGround ground;
        Sprite catSprite;
        Sprite[] catFrames;          // painted kitten animation (if the art exists)
        Sprite particleSprite;
        Texture2D menuBackdrop;
        bool hasKitchenDemo;
        Texture2D levelBackground;   // freed when the level is cleared
        readonly List<Cat> cats = new List<Cat>();
        readonly List<Vector2Int> spawnPoints = new List<Vector2Int>();
        readonly List<RectInt> exitZones = new List<RectInt>();

        // Current level
        int levelNumber;             // 0 = a special level (like the Kitchen demo)
        string levelFile;
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

        // Painted kittens keep their own colours, with just a tiny bit of variety
        static readonly Color[] PaintedTints =
        {
            Color.white,
            new Color(1f, 0.96f, 0.9f),
            new Color(0.94f, 0.95f, 1f),
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
            // Painted kitten: 48x40 frames at 4x detail. Drawn slightly bigger than its 12x10 "body" so it reads well.
            catFrames = Art.SliceSheet(Art.Load("cat_sheet"), 48, 4f / 1.25f, new Vector2(0.5f, 0.02f));
            particleSprite = SpriteMaker.Solid(1, 1, new Color32(255, 255, 255, 255), new Vector2(0.5f, 0.5f));
            menuBackdrop = Art.Load("kitchen_bg");
            Sfx.Init(gameObject);
            levelCount = LevelData.CountLevels();
            hasKitchenDemo = Resources.Load<TextAsset>("Levels/kitchen") != null;
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
            Sfx.Play(saved >= level.need ? "win" : "lose", 1f, 0f);
            if (levelNumber <= 0) return; // special levels don't affect progress
            if (saved > PlayerPrefs.GetInt(BestKey(levelNumber), -1))
                PlayerPrefs.SetInt(BestKey(levelNumber), saved);
            if (saved >= level.need && levelNumber + 1 > Unlocked)
                PlayerPrefs.SetInt(UnlockedKey, levelNumber + 1);
            PlayerPrefs.Save();
        }

        // ---------- Building a level from its file ----------
        void StartLevel(int number) => StartLevel(number, $"level{number:00}");

        void StartLevel(int number, string file)
        {
            var data = LevelData.Load(file);
            if (data == null || data.Rows == 0 || data.Cols == 0)
            {
                Debug.LogError($"Cattas: couldn't load level '{file}' (Assets/Resources/Levels/{file}.txt)");
                mode = GameMode.Menu;
                return;
            }
            levelNumber = number;
            levelFile = file;
            level = data;
            BuildLevel();
            mode = GameMode.Playing;
        }

        void ClearLevel()
        {
            if (levelBackground != null) Destroy(levelBackground);
            levelBackground = null;
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

            // Painted art (optional)
            Texture2D fgArt = Art.Load(level.foreground);
            Texture2D bgArt = Art.Load(level.background);
            levelBackground = bgArt;
            bool painted = fgArt != null;
            int artScale = painted ? Mathf.Max(1, Mathf.RoundToInt(fgArt.width / (float)levelWidth)) : 1;
            // Painted levels sit on a cosy dark frame; plain levels get the sky
            cam.backgroundColor = bgArt != null ? new Color(0.24f, 0.17f, 0.15f) : new Color(0.55f, 0.76f, 0.95f);

            if (bgArt != null)
            {
                var bgObj = new GameObject("Background");
                bgObj.transform.SetParent(levelRoot, false);
                var bsr = bgObj.AddComponent<SpriteRenderer>();
                bsr.sprite = Sprite.Create(bgArt, new Rect(0, 0, bgArt.width, bgArt.height), Vector2.zero, bgArt.width / (float)levelWidth);
                bsr.material = SpriteMaterial;
                bsr.sortingOrder = -10;
            }

            var groundObj = new GameObject("Ground");
            groundObj.transform.SetParent(levelRoot, false);
            ground = groundObj.AddComponent<DiggableGround>();
            ground.Init(levelWidth, levelHeight, artScale);
            if (painted)
            {
                ground.ApplyArt(fgArt);
                Destroy(fgArt);
            }

            const int cs = LevelData.CellSize;
            for (int row = 0; row < level.Rows; row++)
            {
                int y0 = (level.Rows - 1 - row) * cs; // row 0 is the top of the map
                for (int col = 0; col < level.Cols; col++)
                {
                    int x0 = col * cs;
                    char c = level.Cell(col, row);
                    if (c == 'H') { AddHatch(x0 + cs / 2, y0); continue; }
                    if (c == 'E') { AddExit(x0 + cs / 2, y0); continue; }
                    if (painted)
                    {
                        // Painted level: the art decides what's solid; the map only marks stone.
                        if (c == 'X') ground.MakeHard(x0, y0, x0 + cs, y0 + cs);
                        continue;
                    }
                    switch (c)
                    {
                        case '#': ground.FillRect(x0, y0, x0 + cs, y0 + cs); break;
                        case 'X': ground.FillHardRect(x0, y0, x0 + cs, y0 + cs); break;
                        case '/': ground.FillRamp(x0, y0, cs, true); break;
                        case '\\': ground.FillRamp(x0, y0, cs, false); break;
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
            if (level.showMarkers) MakeBox("Hatch", new Vector2Int(centreX - 12, bottomY), 24, 10, new Color32(70, 60, 60, 255), 1);
        }

        void AddExit(int centreX, int floorY)
        {
            if (level.showMarkers)
            {
                MakeBox("Exit House", new Vector2Int(centreX - 10, floorY), 20, 26, new Color32(150, 90, 50, 255), 1);
                MakeBox("Exit Door", new Vector2Int(centreX - 5, floorY), 10, 14, new Color32(40, 25, 20, 255), 2);
            }
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
            baseCamY = camY;
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

        float baseCamY;

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
            TickAlpha = Mathf.Clamp01(tickTimer / tickLength);
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
            if (catFrames != null)
                cat.Init(this, ground, spawn.x, spawn.y, catFrames[0], PaintedTints[released % PaintedTints.Length], catFrames);
            else
                cat.Init(this, ground, spawn.x, spawn.y, catSprite, CatColours[released % CatColours.Length]);
            cats.Add(cat);
            released++;
            Sfx.Play("pop", 0.7f);
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

        // ---------- Effects: particles + sounds ----------
        public void Fx(FxKind kind, int x, int y)
        {
            Color dirt = ground != null ? ground.ColourAt(x, y) : Color.gray;
            switch (kind)
            {
                case FxKind.Dig:
                    Burst(x, y, dirt, 3, 40f, 50f, 1.4f, 0.5f, -260f);
                    Sfx.Play("scratch", 0.35f, 0.07f);
                    break;
                case FxKind.Smash:
                    Burst(x, y, dirt, 4, 70f, 40f, 1.8f, 0.55f, -260f);
                    Sfx.Play("smash", 0.4f, 0.09f);
                    break;
                case FxKind.Hairball:
                    Burst(x, y, new Color(0.62f, 0.55f, 0.47f), 5, 30f, 30f, 1.2f, 0.5f, -120f);
                    Sfx.Play("hack", 0.8f, 0.1f);
                    break;
                case FxKind.Zap:
                    Burst(x, y, dirt, 18, 120f, 80f, 2f, 0.8f, -300f);
                    Burst(x, y, new Color(1f, 0.95f, 0.4f), 12, 140f, 60f, 1.2f, 0.4f, 0f);
                    Shake(0.25f, 3f);
                    Sfx.Play("zap", 1f, 0f);
                    break;
                case FxKind.Splat:
                    Burst(x, y + 3, new Color(1f, 1f, 1f, 0.9f), 10, 60f, 20f, 1.6f, 0.6f, -100f);
                    Sfx.Play("splat", 0.8f, 0.05f);
                    break;
                case FxKind.Puff:
                    Burst(x, y, new Color(1f, 1f, 1f, 0.85f), 8, 35f, 10f, 2f, 0.6f, 0f);
                    Sfx.Play("puff", 0.6f, 0.08f);
                    break;
                case FxKind.Home:
                    Burst(x, y + 6, new Color(1f, 0.55f, 0.65f), 7, 30f, 70f, 1.6f, 1.0f, -40f);
                    Burst(x, y + 6, new Color(1f, 0.92f, 0.45f), 6, 40f, 60f, 1.0f, 0.8f, -40f);
                    Sfx.Play("home", 0.8f, 0.06f);
                    break;
            }
        }

        // Spray some particles out from a point.
        void Burst(float x, float y, Color colour, int count, float spread, float up, float size, float life, float gravity)
        {
            if (levelRoot == null || Particle.Alive > 500) return;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("p");
                go.transform.SetParent(levelRoot, false);
                go.transform.localPosition = new Vector3(x + Random.Range(-2f, 2f), y + Random.Range(-1f, 2f), 0f);
                float s = size * Random.Range(0.6f, 1.3f);
                go.transform.localScale = new Vector3(s, s, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = particleSprite;
                sr.material = SpriteMaterial;
                sr.color = colour * Random.Range(0.85f, 1.1f);
                sr.sortingOrder = 8;
                var p = go.AddComponent<Particle>();
                p.velocity = new Vector2(Random.Range(-spread, spread), Random.Range(up * 0.4f, up * 1.2f));
                p.life = life * Random.Range(0.7f, 1.2f);
                p.gravity = gravity;
                p.spin = Random.Range(-360f, 360f);
            }
        }

        float shakeTime, shakeAmount;
        void Shake(float time, float amount) { shakeTime = time; shakeAmount = amount; }

        void LateUpdate()
        {
            if (cam == null) return;
            if (shakeTime > 0f && mode == GameMode.Playing)
            {
                shakeTime -= Time.deltaTime;
                var p = cam.transform.position;
                cam.transform.position = new Vector3(p.x, baseCamY + Random.Range(-shakeAmount, shakeAmount), p.z);
            }
            else if (levelRoot != null)
            {
                var p = cam.transform.position;
                cam.transform.position = new Vector3(p.x, baseCamY, p.z);
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
            {
                traitsLeft[(int)selectedTrait]--;
                Sfx.Play("click", 0.8f, 0f);
            }
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
        // Colours for the cosy retro-kitchen look
        static readonly Color InkCol = new Color(0.31f, 0.21f, 0.18f);
        static readonly Color CreamCol = new Color(1f, 0.97f, 0.92f, 0.94f);
        static readonly Color MintCol = new Color(0.66f, 0.86f, 0.79f);
        static readonly Color PeachCol = new Color(1f, 0.74f, 0.62f);

        int fs;
        GUIStyle label, small, button, big, panel, selectedButton, darkLabel, darkSmall;
        static Texture2D texPanel, texBtn, texBtnHover, texBtnDown, texBtnSel, texBtnOff;

        void MakeStyles()
        {
            if (texPanel == null)
            {
                texPanel = Art.RoundedBox(CreamCol, new Color(0.55f, 0.42f, 0.36f, 0.9f));
                texBtn = Art.RoundedBox(MintCol, new Color(0.38f, 0.6f, 0.53f));
                texBtnHover = Art.RoundedBox(new Color(0.76f, 0.92f, 0.86f), new Color(0.38f, 0.6f, 0.53f));
                texBtnDown = Art.RoundedBox(new Color(0.55f, 0.78f, 0.7f), new Color(0.3f, 0.5f, 0.44f));
                texBtnSel = Art.RoundedBox(PeachCol, new Color(0.75f, 0.45f, 0.36f), 32, 11f, 3f);
                texBtnOff = Art.RoundedBox(new Color(0.85f, 0.85f, 0.83f, 0.8f), new Color(0.6f, 0.6f, 0.6f, 0.8f));
            }
            fs = Mathf.Max(14, Screen.height / 36);
            var border = new RectOffset(12, 12, 12, 12);

            label = new GUIStyle(GUI.skin.label) { fontSize = fs, fontStyle = FontStyle.Bold };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = Mathf.RoundToInt(fs * 0.8f), fontStyle = FontStyle.Normal, wordWrap = true };
            big = new GUIStyle(label) { fontSize = fs * 2, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            darkLabel = new GUIStyle(label); darkLabel.normal.textColor = InkCol;
            darkSmall = new GUIStyle(small); darkSmall.normal.textColor = InkCol;

            panel = new GUIStyle { border = border };
            panel.normal.background = texPanel;

            button = new GUIStyle { fontSize = fs, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true, border = border,
                                    padding = new RectOffset(6, 6, 4, 4) };
            button.normal.background = texBtn; button.hover.background = texBtnHover; button.active.background = texBtnDown;
            button.normal.textColor = button.hover.textColor = button.active.textColor = InkCol;

            selectedButton = new GUIStyle(button);
            selectedButton.normal.background = selectedButton.hover.background = texBtnSel;
        }

        // Text with a soft shadow, for titles over pictures
        void ShadowLabel(Rect r, string text, GUIStyle style)
        {
            var old = style.normal.textColor;
            style.normal.textColor = new Color(0.25f, 0.15f, 0.12f, 0.6f);
            GUI.Label(new Rect(r.x + fs * 0.12f, r.y + fs * 0.12f, r.width, r.height), text, style);
            style.normal.textColor = old;
            GUI.Label(r, text, style);
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
            if (menuBackdrop != null)
            {
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), menuBackdrop, ScaleMode.ScaleAndCrop);
                var old = GUI.color;
                GUI.color = new Color(1f, 0.97f, 0.92f, 0.35f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = old;
            }

            var title = new GUIStyle(big) { fontSize = fs * 4 };
            title.normal.textColor = new Color(1f, 0.98f, 0.94f);
            ShadowLabel(new Rect(0, Screen.height * 0.04f, Screen.width, fs * 5), "CATTAS", title);
            var sub = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            sub.normal.textColor = new Color(1f, 0.98f, 0.94f);
            ShadowLabel(new Rect(0, Screen.height * 0.04f + fs * 4.4f, Screen.width, fs * 2), "Help the kittens find their way home!", sub);

            // The Kitchen demo
            float dw = fs * 16f, dh = fs * 3.2f;
            var demoRect = new Rect((Screen.width - dw) / 2f, Screen.height * 0.25f, dw, dh);
            if (hasKitchenDemo && GUI.Button(demoRect, "Play the Kitchen Demo", selectedButton))
            {
                StartLevel(0, "kitchen");
                return;
            }

            if (levelCount == 0) return;

            const int perRow = 5;
            float bw = fs * 9f, bh = fs * 3.4f, gap = fs * 0.6f;
            float gridW = perRow * bw + (perRow - 1) * gap;
            float rows = Mathf.Ceil(levelCount / (float)perRow);
            float x0 = (Screen.width - gridW) / 2f;
            float y0 = demoRect.yMax + fs * 2.2f;
            GUI.Box(new Rect(x0 - fs, y0 - fs * 1.9f, gridW + fs * 2, rows * (bh + gap) + fs * 2.4f), GUIContent.none, panel);
            GUI.Label(new Rect(x0, y0 - fs * 1.6f, gridW, fs * 1.4f), "Training levels", darkLabel);

            int unlocked = Unlocked;
            var oldEnabled = GUI.enabled;
            for (int i = 1; i <= levelCount; i++)
            {
                int col = (i - 1) % perRow, row = (i - 1) / perRow;
                var r = new Rect(x0 + col * (bw + gap), y0 + row * (bh + gap), bw, bh);
                bool open = i <= unlocked;
                int best = PlayerPrefs.GetInt(BestKey(i), -1);
                string text = open ? (best >= 0 ? $"Level {i}\nBest: {best}" : $"Level {i}") : $"Level {i}\n(locked)";
                GUI.enabled = open;
                if (GUI.Button(r, text, button)) { GUI.enabled = oldEnabled; StartLevel(i); return; }
            }
            GUI.enabled = oldEnabled;

            // Little helpers for testing
            float hb = fs * 9f, hh = fs * 1.8f;
            var tiny = new GUIStyle(button) { fontSize = Mathf.RoundToInt(fs * 0.7f) };
            if (GUI.Button(new Rect(Screen.width - hb - fs, Screen.height - hh - fs * 0.7f, hb, hh), "Unlock all (test)", tiny))
            {
                PlayerPrefs.SetInt(UnlockedKey, levelCount);
                PlayerPrefs.Save();
            }
            if (GUI.Button(new Rect(Screen.width - hb * 2 - fs * 1.5f, Screen.height - hh - fs * 0.7f, hb, hh), "Reset progress", tiny))
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
            GUI.Box(new Rect(pad, pad, panelW, lineH * 3.6f), GUIContent.none, panel);
            int secs = Mathf.Max(0, Mathf.CeilToInt(ticksLeft / (float)TicksPerSecond));
            GUI.Label(new Rect(pad * 2, pad * 1.3f, panelW, lineH), levelNumber > 0 ? $"Level {levelNumber}: {level.name}" : level.name, darkLabel);
            GUI.Label(new Rect(pad * 2, pad * 1.3f + lineH, panelW, lineH),
                $"Out {released}/{level.cats}   Home {saved} (need {level.need})   Lost {lost}   Time {secs / 60}:{secs % 60:00}", darkSmall);
            GUI.Label(new Rect(pad * 2, pad * 1.3f + lineH * 2f, panelW - pad * 3, lineH * 1.5f), level.hint, darkSmall);

            // Top-right buttons
            float bw = fs * 7;
            if (mode == GameMode.Playing)
            {
                // Speed slider
                float sw = fs * 10f, sx = Screen.width - pad - sw;
                GUI.Box(new Rect(sx - pad * 0.5f, pad * 0.5f, sw + pad, lineH * 1.9f), GUIContent.none, panel);
                GUI.Label(new Rect(sx, pad * 0.5f, sw, lineH), $"Speed {Mathf.RoundToInt(speedPercent)}%",
                    new GUIStyle(darkSmall) { alignment = TextAnchor.MiddleCenter });
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
                ShadowLabel(new Rect(0, pad, Screen.width, lineH), "Scroll: arrow keys / A D, or right-click drag",
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
            var style = new GUIStyle(button) { fontSize = Mathf.RoundToInt(fs * 0.75f) };
            var selStyle = new GUIStyle(selectedButton) { fontSize = style.fontSize };
            var offStyle = new GUIStyle(style);
            offStyle.normal.background = offStyle.hover.background = offStyle.active.background = texBtnOff;
            offStyle.normal.textColor = offStyle.hover.textColor = new Color(0.45f, 0.42f, 0.4f);

            for (int i = 0; i < CatTraits.Count; i++)
            {
                var trait = (CatTrait)i;
                bool usable = CatTraits.IsImplemented(trait) && traitsLeft[i] > 0;
                var st = !usable ? offStyle : trait == selectedTrait ? selStyle : style;
                if (GUI.Button(new Rect(x0 + i * (bw + gap), y0, bw, bh), $"{CatTraits.DisplayName(trait)}\n{traitsLeft[i]}", st) && usable)
                {
                    selectedTrait = trait;
                    Sfx.Play("click", 0.4f, 0f);
                }
            }
        }

        void DrawResults()
        {
            bool won = saved >= level.need;
            var box = new Rect(Screen.width * 0.2f, Screen.height * 0.28f, Screen.width * 0.6f, Screen.height * 0.44f);
            GUI.Box(box, GUIContent.none, panel);

            string msg;
            if (won)
                msg = saved == level.cats ? $"PURR-FECT!\nAll {saved} cats made it home!" : $"Level complete!\n{saved} cats home (needed {level.need})";
            else
                msg = (timeUp ? "Out of time!\n" : "") + $"Only {saved} cats made it home...\nYou need {level.need}.";
            var bigDark = new GUIStyle(big); bigDark.normal.textColor = InkCol;
            GUI.Label(new Rect(box.x, box.y, box.width, box.height * 0.65f), msg, bigDark);

            float bw = fs * 8f, bh = fs * 2f, by = box.yMax - bh * 1.5f;
            bool hasNext = won && levelNumber > 0 && levelNumber < levelCount;
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
