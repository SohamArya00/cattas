using System.Collections.Generic;
using UnityEngine;

namespace Cattas
{
    public enum FxKind { Dig, Smash, Hairball, Zap, Splat, Puff, Home, Soak, Burn, Knock, Secret, Switch, Snap }

    // The "brain" of the game. It starts itself automatically when you press Play,
    // so there is nothing to set up in the Unity editor.
    //
    // Flow: Menu -> Preview (look around the level) -> Playing -> Results -> Menu / next level.
    public class GameBootstrap : MonoBehaviour
    {
        public const int TicksPerSecond = 30; // game logic runs at a steady 30 steps a second

        // How far we are between one game step and the next (0..1) - used to make movement smooth.
        public static float TickAlpha { get; private set; }

        enum GameMode { Menu, Preview, Playing, Results }
        GameMode mode = GameMode.Menu;

        Camera cam;
        Transform levelRoot;
        DiggableGround ground;
        Sprite catSprite;
        Sprite[] catFrames;          // painted kitten animation (if the art exists)
        public CatRig KittenRig { get; private set; }   // paper-doll kitten (preferred when available)
        Sprite particleSprite, flashSprite;
        Texture2D menuBackdrop;
        bool hasKitchenDemo;
        readonly List<Texture2D> levelTextures = new List<Texture2D>(); // freed when the level is cleared
        Texture2D levelBackground;

        readonly List<Cat> cats = new List<Cat>();
        readonly List<Vector2Int> spawnPoints = new List<Vector2Int>();
        readonly List<int> spawnDirs = new List<int>();   // 1 = kittens walk right, -1 = left
        readonly List<RectInt> exitZones = new List<RectInt>();

        // Current level
        int levelNumber;             // 0 = a special level (like the Kitchen demo)
        string levelFile = "";
        int levelCount;
        LevelData level;
        int levelWidth = 640, levelHeight = 360;
        readonly int[] traitsLeft = new int[CatTraits.Count];
        CatTrait selectedTrait = CatTrait.LitterLord;

        int released, saved, lost, ticksUntilNextCat, ticksLeft, elapsedTicks;
        bool timeUp;
        float tickTimer;

        // Game speed in %: 50 = normal, 100 = double speed.
        // Players can only speed up from the level's starting speed (slower than normal will be an NG+ unlock).
        float speedPercent = 50f;
        int minSpeedPercent = 50;

        // Saved progress (stays between play sessions)
        const string UnlockedKey = "cattas_unlocked";
        static string BestKey(int n) => $"cattas_best_{n}";
        static string GoalsKey(string file) => $"cattas_goals_{file}";
        static string SecretKey(string file, string title) => $"cattas_secret_{file}_{title}";

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
            // Painted kitten sheet: 6 frames side by side, each 1.2x as wide as it is tall (any resolution).
            // Each frame is shown 15 level-pixels wide - a bit bigger than the kitten's 12x10 "body" so it reads well.
            var sheet = Art.Load("cat_sheet");
            if (sheet != null)
            {
                int frameW = Mathf.RoundToInt(sheet.height * 1.2f);
                catFrames = Art.SliceSheet(sheet, frameW, frameW / 15f, new Vector2(0.5f, 0.02f));
            }
            KittenRig = CatRig.Load("kitten");
            particleSprite = SpriteMaker.Solid(1, 1, new Color32(255, 255, 255, 255), new Vector2(0.5f, 0.5f));
            flashSprite = SpriteMaker.Circle(14, new Color32(255, 240, 120, 255));
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
            camPos = new Vector2(320f, 180f);
            viewH = 360f;
            ApplyCamera();
        }

        // ---------- Progress ----------
        int Unlocked => Mathf.Max(1, PlayerPrefs.GetInt(UnlockedKey, 1));

        void RecordResult()
        {
            Sfx.Play(saved >= level.need ? "win" : "lose", 1f, 0f);
            int mask = 0;
            var goals = Goals();
            for (int i = 0; i < goals.Count; i++) if (goals[i].done) mask |= 1 << i;
            PlayerPrefs.SetInt(GoalsKey(levelFile), PlayerPrefs.GetInt(GoalsKey(levelFile), 0) | mask);

            if (levelNumber > 0)
            {
                if (saved > PlayerPrefs.GetInt(BestKey(levelNumber), -1))
                    PlayerPrefs.SetInt(BestKey(levelNumber), saved);
                if (saved >= level.need && levelNumber + 1 > Unlocked)
                    PlayerPrefs.SetInt(UnlockedKey, levelNumber + 1);
            }
            PlayerPrefs.Save();
        }

        // ---------- Objectives ----------
        struct Goal { public string text; public bool done; }

        List<Goal> Goals()
        {
            var list = new List<Goal>();
            bool won = saved >= level.need;
            list.Add(new Goal { text = $"Get {level.need} kittens home", done = won });
            list.Add(new Goal { text = $"Save every kitten ({level.cats})", done = saved == level.cats });
            if (level.parSeconds > 0)
                list.Add(new Goal { text = $"Finish in under {level.parSeconds / 60}:{level.parSeconds % 60:00}",
                                    done = won && elapsedTicks <= level.parSeconds * TicksPerSecond });
            if (secretsTotal > 0)
                list.Add(new Goal { text = $"Find all {secretsTotal} secrets", done = secretsFound == secretsTotal });
            return list;
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
            BuildLevel(true);
        }

        void ClearLevel()
        {
            foreach (var t in levelTextures) if (t != null) Destroy(t);
            levelTextures.Clear();
            levelBackground = null;
            if (levelRoot != null) Destroy(levelRoot.gameObject);
            levelRoot = null;
            ground = null;
            cats.Clear();
            things.Clear();
            popups.Clear();
        }

        // preview = start zoomed out so the player can look around first
        void BuildLevel(bool preview)
        {
            ClearLevel();
            spawnPoints.Clear();
            spawnDirs.Clear();
            exitZones.Clear();
            released = saved = lost = elapsedTicks = 0;
            secretsFound = secretsTotal = 0;
            timeUp = false;
            ticksUntilNextCat = TicksPerSecond; // first cat after 1 second
            ticksLeft = level.timeSeconds * TicksPerSecond;
            for (int i = 0; i < traitsLeft.Length; i++) traitsLeft[i] = level.traits[i];
            selectedTrait = FirstAvailableTrait();
            minSpeedPercent = level.speed;
            speedPercent = level.speed;

            levelWidth = level.PixelWidth;
            levelHeight = level.PixelHeight;

            levelRoot = new GameObject("Level " + levelFile).transform;
            levelRoot.SetParent(transform, false);

            // Painted art (optional)
            Texture2D fgArt = Art.Load(level.foreground);
            Texture2D bgArt = Art.Load(level.background);
            bool painted = fgArt != null;
            int artScale = painted ? Mathf.Max(1, Mathf.RoundToInt(fgArt.width / (float)levelWidth)) : 1;
            // Painted levels sit on a cosy dark frame; plain levels get the sky
            cam.backgroundColor = bgArt != null ? new Color(0.24f, 0.17f, 0.15f) : new Color(0.55f, 0.76f, 0.95f);

            if (bgArt != null)
            {
                levelTextures.Add(bgArt);
                levelBackground = bgArt;
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
                    if (c == 'H' || c == 'h') { AddHatch(x0 + cs / 2, y0, c == 'h' ? -1 : 1); continue; }
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

            BuildThings();

            // Camera: preview starts zoomed right out; otherwise jump straight to the hatch
            camFlying = false;
            if (preview)
            {
                viewH = MaxView;
                camPos = new Vector2(levelWidth / 2f, levelHeight / 2f);
                mode = GameMode.Preview;
            }
            else
            {
                viewH = PlayView;
                camPos = HatchView(viewH);
                mode = GameMode.Playing;
            }
            ClampCam();
            ApplyCamera();
        }

        void AddHatch(int centreX, int bottomY, int dir = 1)
        {
            spawnPoints.Add(new Vector2Int(centreX, bottomY - 2));
            spawnDirs.Add(dir);
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

        GameObject MakeBox(string name, Vector2Int pos, int w, int h, Color32 colour, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(levelRoot, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteMaker.Solid(w, h, colour, Vector2.zero);
            sr.material = SpriteMaterial;
            sr.sortingOrder = order;
            return go;
        }

        CatTrait FirstAvailableTrait()
        {
            for (int i = 0; i < traitsLeft.Length; i++)
                if (traitsLeft[i] > 0) return (CatTrait)i;
            return CatTrait.LitterLord;
        }

        // ---------- Level objects: water, fire, switches, props, secrets ----------
        class Thing
        {
            public LevelObject data;
            public bool on = true;          // fire lit / switch unused / prop standing / secret uncollected
            public GameObject go;
            public SpriteRenderer sr;
            public float timer;
        }

        readonly List<Thing> things = new List<Thing>();
        int secretsFound, secretsTotal;

        void BuildThings()
        {
            foreach (var o in level.objects)
            {
                var t = new Thing { data = o };
                var r = o.rect;
                switch (o.type)
                {
                    case "water":
                        t.go = MakeBox("Water", new Vector2Int(r.x, r.y), Mathf.Max(1, r.width), Mathf.Max(1, r.height),
                                       new Color32(120, 180, 235, 150), 6);
                        var surface = MakeBox("Water surface", new Vector2Int(r.x, r.yMax - 2), Mathf.Max(1, r.width), 2,
                                              new Color32(220, 240, 255, 200), 6);
                        surface.transform.SetParent(t.go.transform, true);
                        break;
                    case "fire":
                        t.go = MakeBox("Fire glow", new Vector2Int(r.x - 2, r.y), r.width + 4, Mathf.Max(2, r.height / 2),
                                       new Color32(255, 150, 60, 70), 4);
                        break;
                    case "switch":
                    case "prop":
                    case "secret":
                    case "trap":
                        t.go = MakeArtThing(o, o.type == "prop" ? 7 : o.type == "switch" || o.type == "trap" ? 4 : 3);
                        if (o.type == "secret")
                        {
                            secretsTotal++;
                            // already found on an earlier play? show it slightly faded (it can still be collected)
                            if (t.go != null && PlayerPrefs.GetInt(SecretKey(levelFile, o.title), 0) == 1)
                                t.go.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 0.6f);
                        }
                        break;
                }
                if (t.go != null) t.sr = t.go.GetComponent<SpriteRenderer>();
                things.Add(t);
            }
        }

        GameObject MakeArtThing(LevelObject o, int order)
        {
            var tex = Art.Load(o.art);
            var r = o.rect;
            var go = new GameObject(o.type + " " + o.art);
            go.transform.SetParent(levelRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.material = SpriteMaterial;
            sr.sortingOrder = order;
            if (tex != null)
            {
                levelTextures.Add(tex);
                // centre the picture on the object's rectangle, scaled to its width
                float ppu = tex.width / Mathf.Max(1f, r.width);
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), ppu);
                go.transform.localPosition = new Vector3(r.x + r.width / 2f, r.y, 0f);
            }
            else
            {
                sr.sprite = SpriteMaker.Solid(Mathf.Max(1, r.width), Mathf.Max(1, r.height), new Color32(255, 220, 90, 255), new Vector2(0.5f, 0f));
                go.transform.localPosition = new Vector3(r.x + r.width / 2f, r.y, 0f);
            }
            return go;
        }

        static bool FeetIn(RectInt r, int x, int y) => x >= r.xMin && x < r.xMax && y >= r.yMin && y < r.yMax;
        static bool BodyTouches(RectInt r, int x, int y) => x >= r.xMin - 2 && x < r.xMax + 2 && y < r.yMax && y + 11 > r.yMin;

        bool IsCovered(Thing secret)
        {
            foreach (var t in things)
                if (t.data.type == "prop" && t.on && t.data.rect.Overlaps(secret.data.rect)) return true;
            return false;
        }

        // Called every game step: see what the kittens are touching.
        void CheckThings()
        {
            if (things.Count == 0) return;
            var snapshot = new List<Cat>(cats);
            foreach (var cat in snapshot)
            {
                if (!cat.IsActive) continue;
                int x = cat.X, y = cat.Y;
                foreach (var t in things)
                {
                    if (!cat.IsActive) break;
                    var r = t.data.rect;
                    switch (t.data.type)
                    {
                        case "water":
                            if (FeetIn(r, x, y)) cat.Hazard(FxKind.Soak);
                            break;
                        case "fire":
                            if (t.on && FeetIn(r, x, y)) cat.Hazard(FxKind.Burn);
                            break;
                        case "trap":
                            if (t.on && FeetIn(r, x, y))
                            {
                                cat.Hazard(FxKind.Snap);
                                t.timer = 0.35f; // little "snap" bounce
                            }
                            break;
                        case "switch":
                            if (t.on && BodyTouches(r, x, y)) FlipSwitch(t);
                            break;
                        case "prop":
                            if (t.on && BodyTouches(r, x, y)) KnockOver(t, cat.Dir);
                            break;
                        case "secret":
                            if (t.on && !IsCovered(t) && BodyTouches(r, x, y)) Collect(t);
                            break;
                    }
                }
            }
        }

        void FlipSwitch(Thing t)
        {
            t.on = false;
            if (t.go != null) t.go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            int turnedOff = 0;
            foreach (var f in things)
                if (f.data.type == "fire" && f.data.group == t.data.group && f.on)
                {
                    f.on = false;
                    turnedOff++;
                    if (f.go != null) f.go.SetActive(false);
                    var c = f.data.rect.center;
                    Burst(c.x, c.y, new Color(0.85f, 0.85f, 0.85f, 0.8f), 10, 25f, 30f, 2.5f, 1f, 30f);
                }
            Fx(FxKind.Switch, t.data.rect.x, t.data.rect.y);
            string what = string.IsNullOrEmpty(t.data.group) ? "Switch" : char.ToUpper(t.data.group[0]) + t.data.group.Substring(1);
            Popup(turnedOff > 0 ? $"{what} turned off!" : $"Click!");
        }

        void KnockOver(Thing t, int dir)
        {
            t.on = false;
            Fx(FxKind.Knock, (int)t.data.rect.center.x, t.data.rect.y + 4);
            if (t.go == null) return;
            var p = t.go.AddComponent<Particle>();
            p.velocity = new Vector2(dir * 45f, 70f);
            p.gravity = -380f;
            p.life = 1.6f;
            p.spin = -dir * 260f;
        }

        void Collect(Thing t)
        {
            t.on = false;
            secretsFound++;
            var c = t.data.rect.center;
            Fx(FxKind.Secret, (int)c.x, (int)c.y);
            if (t.go != null)
            {
                var p = t.go.AddComponent<FadeAway>();
                p.life = 0.8f;
                p.rise = 30f;
                p.grow = 1f;
            }
            PlayerPrefs.SetInt(SecretKey(levelFile, t.data.title), 1);
            PlayerPrefs.Save();
            Popup($"Secret found: {t.data.title}!  ({secretsFound}/{secretsTotal})");
        }

        // A Cable Chewer's ZAP breaks any mouse traps close by
        void DisarmTrapsNear(int x, int y, int radius)
        {
            foreach (var t in things)
            {
                if (t.data.type != "trap" || !t.on) continue;
                var c = t.data.rect.center;
                if ((c - new Vector2(x, y)).sqrMagnitude > radius * radius) continue;
                t.on = false;
                if (t.sr != null) t.sr.color = new Color(0.5f, 0.45f, 0.4f, 0.8f);
                if (t.go != null) t.go.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
                Popup("Mouse trap disarmed!");
            }
        }

        // Every frame: flames flicker, water bobs, secrets twinkle.
        void AnimateThings()
        {
            foreach (var t in things)
            {
                var r = t.data.rect;
                switch (t.data.type)
                {
                    case "fire":
                        if (!t.on) break;
                        t.timer += Time.deltaTime * Mathf.Max(8f, r.width * 1.6f);
                        while (t.timer > 1f)
                        {
                            t.timer -= 1f;
                            var col = Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.1f), Random.value);
                            SpawnParticle(new Vector2(Random.Range(r.xMin, r.xMax), r.yMin + Random.Range(0f, 3f)),
                                          new Vector2(Random.Range(-6f, 6f), Random.Range(20f, 45f)), col, Random.Range(1.5f, 3.2f), Random.Range(0.25f, 0.5f), 40f, 9);
                        }
                        if (t.sr != null) t.sr.color = new Color(1f, 0.6f, 0.25f, 0.2f + 0.1f * Mathf.Sin(Time.time * 17f));
                        break;
                    case "trap":
                        if (t.go == null) break;
                        if (t.timer > 0f)
                        {
                            t.timer -= Time.deltaTime;
                            float k = 1f + 0.3f * Mathf.Sin(t.timer * 40f) * t.timer;
                            t.go.transform.localScale = new Vector3(1f, k, 1f);
                        }
                        else t.go.transform.localScale = Vector3.one;
                        break;
                    case "water":
                        if (t.go != null) t.go.transform.localPosition = new Vector3(r.x, r.y + Mathf.Sin(Time.time * 2.5f) * 0.6f, 0f);
                        break;
                    case "secret":
                        if (!t.on || IsCovered(t)) break;
                        t.timer += Time.deltaTime;
                        if (t.timer > 0.35f)
                        {
                            t.timer = 0f;
                            SpawnParticle(new Vector2(Random.Range(r.xMin, r.xMax), Random.Range(r.yMin, r.yMax)),
                                          new Vector2(0f, 8f), new Color(1f, 0.95f, 0.5f), 1.2f, 0.6f, 0f, 9);
                        }
                        break;
                }
            }
        }

        // ---------- Popup messages ----------
        struct PopupMsg { public string text; public float time; }
        readonly List<PopupMsg> popups = new List<PopupMsg>();
        void Popup(string text) => popups.Add(new PopupMsg { text = text, time = Time.unscaledTime });

        // ---------- Camera ----------
        Vector2 camPos;           // what the camera is looking at (centre), in level pixels
        float viewH = 360f;       // how much of the level is visible, top to bottom
        bool camFlying;           // swooping in after the preview
        Vector2 flyTarget;
        Vector2 shakeOffset;
        float shakeTime, shakeAmount;
        const float BottomUI = 0.13f; // part of the screen covered by the trait bar

        float MinView => 140f;
        float MaxView => Mathf.Max(levelHeight / (1f - BottomUI) * 1.02f, levelWidth / cam.aspect);
        float PlayView => levelHeight <= 400 ? MaxView : Mathf.Min(MaxView, 250f);

        // Where the camera looks when play starts: the hatch, a little ahead of it
        Vector2 HatchView(float vh)
        {
            var h = spawnPoints.Count > 0 ? spawnPoints[0] : new Vector2Int(levelWidth / 2, levelHeight / 2);
            int d = spawnDirs.Count > 0 ? spawnDirs[0] : 1;
            return ClampPos(new Vector2(h.x + d * vh * 0.4f, h.y - vh * 0.15f), vh);
        }

        void ClampCam()
        {
            viewH = Mathf.Clamp(viewH, MinView, Mathf.Max(MinView, MaxView));
            camPos = ClampPos(camPos, viewH);
        }

        // Keep the view inside the level (with room at the bottom for the trait bar)
        Vector2 ClampPos(Vector2 p, float vh)
        {
            float halfH = vh / 2f, halfW = halfH * cam.aspect;
            if (levelWidth <= halfW * 2f) p.x = levelWidth / 2f;
            else p.x = Mathf.Clamp(p.x, halfW, levelWidth - halfW);
            float minY = halfH - vh * BottomUI;
            float maxY = levelHeight + vh * 0.02f - halfH;
            p.y = minY > maxY ? (minY + maxY) / 2f : Mathf.Clamp(p.y, minY, maxY);
            return p;
        }

        void ApplyCamera()
        {
            if (cam == null) return;
            cam.orthographicSize = viewH / 2f;
            cam.transform.position = new Vector3(camPos.x + shakeOffset.x, camPos.y + shakeOffset.y, -10f);
        }

        void ZoomAt(float factor, Vector2 screenPoint)
        {
            Vector3 before = cam.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, 0f));
            viewH *= factor;
            ClampCam();
            ApplyCamera();
            Vector3 after = cam.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, 0f));
            camPos += new Vector2(before.x - after.x, before.y - after.y);
            ClampCam();
            ApplyCamera();
            camFlying = false;
        }

        void StartPlaying()
        {
            mode = GameMode.Playing;
            camFlying = true;
            flyTarget = HatchView(PlayView);
        }

        // Keyboard: arrows / WASD to scroll, + and - to zoom
        void KeyboardCamera()
        {
            float h = 0f, v = 0f, z = 0f;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) h -= 1f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) h += 1f;
            if (kb.upArrowKey.isPressed || kb.wKey.isPressed) v += 1f;
            if (kb.downArrowKey.isPressed || kb.sKey.isPressed) v -= 1f;
            if (kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed) z -= 1f;
            if (kb.minusKey.isPressed || kb.numpadMinusKey.isPressed) z += 1f;
#else
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) h -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) h += 1f;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) v += 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) v -= 1f;
            if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus)) z -= 1f;
            if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus)) z += 1f;
#endif
            if (h != 0f || v != 0f)
            {
                camPos += new Vector2(h, v) * viewH * 1.1f * Time.unscaledDeltaTime;
                camFlying = false;
                ClampCam();
            }
            if (z != 0f)
                ZoomAt(1f + z * 1.5f * Time.unscaledDeltaTime, new Vector2(Screen.width / 2f, Screen.height / 2f));
        }

        // ---------- Game loop ----------
        void Update()
        {
            if (mode == GameMode.Menu || levelRoot == null) return;

            KeyboardCamera();
            if (camFlying)
            {
                float k = 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime);
                viewH = Mathf.Lerp(viewH, PlayView, k);
                camPos = Vector2.Lerp(camPos, flyTarget, k);
                ClampCam();
                if (Mathf.Abs(viewH - PlayView) < 0.5f && (camPos - flyTarget).sqrMagnitude < 1f) camFlying = false;
            }
            AnimateThings();

            if (mode != GameMode.Playing) { TickAlpha = 1f; return; }

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

        void LateUpdate()
        {
            if (cam == null) return;
            if (shakeTime > 0f)
            {
                shakeTime -= Time.deltaTime;
                shakeOffset = Random.insideUnitCircle * shakeAmount;
            }
            else shakeOffset = Vector2.zero;
            if (mode != GameMode.Menu) ApplyCamera();
        }

        void GameTick()
        {
            if (released < level.cats && --ticksUntilNextCat <= 0)
            {
                SpawnCat();
                ticksUntilNextCat = Mathf.Max(1, Mathf.RoundToInt(level.rate * TicksPerSecond));
            }
            for (int i = cats.Count - 1; i >= 0; i--)
                if (i < cats.Count) cats[i].Tick();
            CheckThings();

            elapsedTicks++;
            if (--ticksLeft <= 0) timeUp = true;

            if (IsFinished)
            {
                RecordResult();
                mode = GameMode.Results;
            }
        }

        void SpawnCat()
        {
            int hatch = released % spawnPoints.Count;
            var spawn = spawnPoints[hatch];
            int dir = spawnDirs[hatch];
            var go = new GameObject("Cat " + (released + 1));
            go.transform.SetParent(levelRoot, false);
            var cat = go.AddComponent<Cat>();
            if (catFrames != null)
                cat.Init(this, ground, spawn.x, spawn.y, catFrames[0], PaintedTints[released % PaintedTints.Length], catFrames, dir);
            else
                cat.Init(this, ground, spawn.x, spawn.y, catSprite, CatColours[released % CatColours.Length], null, dir);
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
                    DisarmTrapsNear(x, y, 22);
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
                case FxKind.Soak:
                    Burst(x, y + 2, new Color(0.6f, 0.82f, 1f, 0.9f), 14, 45f, 90f, 1.4f, 0.7f, -300f);
                    Sfx.Play("splash", 0.9f, 0.05f);
                    break;
                case FxKind.Burn:
                    Burst(x, y + 4, new Color(1f, 0.6f, 0.2f), 10, 40f, 70f, 1.4f, 0.5f, -100f);
                    Burst(x, y + 6, new Color(0.45f, 0.42f, 0.4f, 0.8f), 10, 15f, 40f, 3f, 1.2f, 20f);
                    Sfx.Play("sizzle", 0.9f, 0.05f);
                    break;
                case FxKind.Knock:
                    Burst(x, y, new Color(0.85f, 0.65f, 0.45f), 8, 50f, 40f, 1.4f, 0.7f, -250f);
                    Sfx.Play("clatter", 0.9f, 0.05f);
                    break;
                case FxKind.Secret:
                    Burst(x, y, new Color(1f, 0.9f, 0.35f), 24, 70f, 70f, 1.6f, 1.2f, -30f);
                    Burst(x, y, Color.white, 10, 40f, 50f, 1f, 1f, -10f);
                    Sfx.Play("secret", 1f, 0f);
                    break;
                case FxKind.Switch:
                    Sfx.Play("clunk", 1f, 0f);
                    break;
                case FxKind.Snap:
                    Burst(x, y + 2, new Color(0.85f, 0.65f, 0.45f), 6, 40f, 60f, 1.2f, 0.5f, -250f);
                    Shake(0.12f, 1.5f);
                    Sfx.Play("snap", 1f, 0.05f);
                    break;
            }
        }

        // Spray some particles out from a point.
        void Burst(float x, float y, Color colour, int count, float spread, float up, float size, float life, float gravity)
        {
            for (int i = 0; i < count; i++)
                SpawnParticle(new Vector2(x + Random.Range(-2f, 2f), y + Random.Range(-1f, 2f)),
                              new Vector2(Random.Range(-spread, spread), Random.Range(up * 0.4f, up * 1.2f)),
                              colour * Random.Range(0.85f, 1.1f), size * Random.Range(0.6f, 1.3f), life * Random.Range(0.7f, 1.2f), gravity, 8);
        }

        void SpawnParticle(Vector2 pos, Vector2 velocity, Color colour, float size, float life, float gravity, int order)
        {
            if (levelRoot == null || Particle.Alive > 600) return;
            var go = new GameObject("p");
            go.transform.SetParent(levelRoot, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            go.transform.localScale = new Vector3(size, size, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = particleSprite;
            sr.material = SpriteMaterial;
            sr.color = colour;
            sr.sortingOrder = order;
            var p = go.AddComponent<Particle>();
            p.velocity = velocity;
            p.life = life;
            p.gravity = gravity;
            p.spin = Random.Range(-360f, 360f);
        }

        void Shake(float time, float amount) { shakeTime = time; shakeAmount = amount; }

        // ZAP! A bright flash, and a frizzy cat that shoots off home to recover.
        public void SpawnZap(Cat cat)
        {
            var flash = new GameObject("Zap Flash");
            flash.transform.SetParent(levelRoot, false);
            flash.transform.localPosition = new Vector3(cat.X + 0.5f, cat.Y + 5f, 0f);
            var fsr = flash.AddComponent<SpriteRenderer>();
            fsr.sprite = flashSprite;
            fsr.material = SpriteMaterial;
            fsr.sortingOrder = 10;
            var ffade = flash.AddComponent<FadeAway>();
            ffade.life = 0.3f;
            ffade.grow = 0.6f;
            SpawnGhostCat(cat, cat.Tint * 0.45f + new Color(0f, 0f, 0f, 0.55f), 1.5f, 60f, 1.2f, CatState.Falling);
        }

        // A soaked, singed or startled kitten heading home to recover.
        public void SpawnSadCat(Cat cat, FxKind kind)
        {
            if (kind == FxKind.Soak) SpawnGhostCat(cat, new Color(0.6f, 0.75f, 1f, 1f), 0.9f, 25f, 1.6f, CatState.Splatted);
            else if (kind == FxKind.Snap) SpawnGhostCat(cat, new Color(1f, 0.9f, 0.85f, 1f), 1.2f, 150f, 0.7f, CatState.Falling);  // boing!
            else SpawnGhostCat(cat, new Color(0.35f, 0.3f, 0.3f, 1f), 1.3f, 70f, 1.2f, CatState.Zooming);                       // singed - off it runs
        }

        void SpawnGhostCat(Cat cat, Color colour, float scale, float rise, float life, CatState pose)
        {
            if (levelRoot == null) return;
            var go = new GameObject("Leaving Cat");
            go.transform.SetParent(levelRoot, false);
            go.transform.localPosition = new Vector3(cat.X + 0.5f, cat.Y, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            if (KittenRig != null)
            {
                var puppet = CatPuppet.Create(go.transform, KittenRig, colour, 11);
                puppet.state = pose;
                puppet.flip = cat.Dir < 0;
                var fade = puppet.gameObject.AddComponent<FadeGroup>();
                fade.life = life;
                fade.rise = 0f;
                var mover = go.AddComponent<FadeGroupMover>();
                mover.rise = rise;
                mover.life = life;
                return;
            }
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = cat.StillSprite != null ? cat.StillSprite : catSprite;
            sr.color = colour;
            sr.material = SpriteMaterial;
            sr.sortingOrder = 11;
            var f = go.AddComponent<FadeAway>();
            f.life = life;
            f.rise = rise;
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
            // a little more forgiving when zoomed out
            float slack = Mathf.Max(1f, viewH / 250f);
            Cat best = null;
            float bestDist = float.MaxValue;
            foreach (var cat in cats)
            {
                float dx = Mathf.Abs(world.x - (cat.X + 0.5f));
                float dy = world.y - cat.Y;
                if (dx > 7f * slack || dy < -3f * slack || dy > 14f * slack) continue;
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
            DrawFrame(r, Mathf.Max(2f, Screen.height / 400f), colour);
        }

        static void DrawFrame(Rect r, float t, Color colour)
        {
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
        static Texture2D texPanel, texBtn, texBtnHover, texBtnDown, texBtnSel, texBtnOff, texTick, texBox;

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
                texTick = Art.RoundedBox(PeachCol, new Color(0.75f, 0.45f, 0.36f), 32, 9f, 3f);
                texBox = Art.RoundedBox(new Color(1f, 1f, 1f, 0.5f), new Color(0.55f, 0.42f, 0.36f), 32, 9f, 3f);
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
                case GameMode.Preview: DrawPreview(); break;
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
            if (hasKitchenDemo)
            {
                int trophies = CountBits(PlayerPrefs.GetInt(GoalsKey("kitchen"), 0));
                if (GUI.Button(demoRect, trophies > 0 ? $"Play the Kitchen Demo\n<size={fs * 2 / 3}>Trophies: {trophies}</size>" : "Play the Kitchen Demo",
                               new GUIStyle(selectedButton) { richText = true }))
                {
                    StartLevel(0, "kitchen");
                    return;
                }
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
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
            }
        }

        static int CountBits(int v) { int n = 0; while (v != 0) { n += v & 1; v >>= 1; } return n; }

        // Look around the level before starting
        void DrawPreview()
        {
            float pad = fs * 0.6f;
            DrawMinimap(pad);
            HandleCameraMouse();

            float w = fs * 24f, lineH = fs * 1.5f;
            var goals = Goals();
            int best = PlayerPrefs.GetInt(GoalsKey(levelFile), 0);
            float h = lineH * (4.4f + goals.Count) + fs * 3.2f;
            var box = new Rect(pad, Screen.height - h - pad, w, h);
            GUI.Box(box, GUIContent.none, panel);
            float y = box.y + pad;
            GUI.Label(new Rect(box.x + pad * 1.5f, y, w - pad * 3, lineH), levelNumber > 0 ? $"Level {levelNumber}: {level.name}" : level.name, darkLabel);
            y += lineH;
            GUI.Label(new Rect(box.x + pad * 1.5f, y, w - pad * 3, lineH * 2.2f), level.hint, darkSmall);
            y += lineH * 2.3f;
            GUI.Label(new Rect(box.x + pad * 1.5f, y, w - pad * 3, lineH), "Goals", darkLabel);
            y += lineH;
            for (int i = 0; i < goals.Count; i++)
            {
                DrawTick(new Rect(box.x + pad * 1.5f, y + fs * 0.15f, fs * 1.1f, fs * 1.1f), (best & (1 << i)) != 0);
                GUI.Label(new Rect(box.x + pad * 1.5f + fs * 1.6f, y, w - fs * 4, lineH), goals[i].text, darkSmall);
                y += lineH;
            }
            if (GUI.Button(new Rect(box.x + pad * 1.5f, box.yMax - fs * 2.8f, w - pad * 3, fs * 2.2f), "Let's go!", selectedButton))
            {
                StartPlaying();
                return;
            }

            var tip = new GUIStyle(small) { alignment = TextAnchor.UpperCenter };
            ShadowLabel(new Rect(0, Screen.height - fs * 2.2f, Screen.width, fs * 2f),
                        "Look around:  scroll wheel to zoom,  right-drag or WASD / arrows to move,  click the map to jump", tip);
            if (GUI.Button(new Rect(Screen.width - pad - fs * 7, pad, fs * 7, fs * 1.6f), "Levels", button)) { ClearLevel(); mode = GameMode.Menu; }
        }

        void DrawTick(Rect r, bool done) => GUI.DrawTexture(r, done ? texTick : texBox);

        // Mouse wheel zoom + right/middle drag to move the camera
        void HandleCameraMouse()
        {
            Event e = Event.current;
            if (e.type == EventType.ScrollWheel)
            {
                float f = Mathf.Clamp(1f + e.delta.y * 0.05f, 0.7f, 1.4f);
                ZoomAt(f, new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y));
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && (e.button == 1 || e.button == 2))
            {
                float worldPerPixel = viewH / Screen.height;
                camPos += new Vector2(-e.delta.x, e.delta.y) * worldPerPixel;
                camFlying = false;
                ClampCam();
                e.Use();
            }
        }

        // Small map of the whole level. Click or drag on it to move the camera.
        void DrawMinimap(float pad)
        {
            if (ground == null) return;
            bool big = levelWidth > 700 || levelHeight > 420;
            if (!big) return;
            float mw = fs * 13f;
            float mh = mw * levelHeight / levelWidth;
            if (mh > fs * 7.5f) { mh = fs * 7.5f; mw = mh * levelWidth / levelHeight; }
            var outer = new Rect((Screen.width - mw) / 2f - pad * 0.6f, pad * 0.5f, mw + pad * 1.2f, mh + pad * 1.2f);
            var r = new Rect(outer.x + pad * 0.6f, outer.y + pad * 0.6f, mw, mh);
            GUI.Box(outer, GUIContent.none, panel);
            if (levelBackground != null) GUI.DrawTexture(r, levelBackground, ScaleMode.StretchToFill);
            else { var o = GUI.color; GUI.color = cam.backgroundColor; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = o; }
            if (ground.Texture != null) GUI.DrawTexture(r, ground.Texture, ScaleMode.StretchToFill);

            // kittens
            var old = GUI.color;
            GUI.color = new Color(1f, 0.45f, 0.55f);
            float d = Mathf.Max(3f, fs * 0.25f);
            foreach (var cat in cats)
            {
                float cx = r.x + cat.X / (float)levelWidth * mw;
                float cy = r.yMax - cat.Y / (float)levelHeight * mh;
                GUI.DrawTexture(new Rect(cx - d / 2, cy - d, d, d), Texture2D.whiteTexture);
            }
            GUI.color = old;

            // what the camera can see
            float halfH = viewH / 2f, halfW = halfH * cam.aspect;
            var view = Rect.MinMaxRect(
                r.x + Mathf.Clamp01((camPos.x - halfW) / levelWidth) * mw,
                r.yMax - Mathf.Clamp01((camPos.y + halfH) / levelHeight) * mh,
                r.x + Mathf.Clamp01((camPos.x + halfW) / levelWidth) * mw,
                r.yMax - Mathf.Clamp01((camPos.y - halfH) / levelHeight) * mh);
            DrawFrame(view, 2f, Color.white);

            Event e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && r.Contains(e.mousePosition))
            {
                camPos = new Vector2((e.mousePosition.x - r.x) / mw * levelWidth, (r.yMax - e.mousePosition.y) / mh * levelHeight);
                camFlying = false;
                ClampCam();
                e.Use();
            }
        }

        void DrawPlaying()
        {
            float pad = fs * 0.6f, lineH = fs * 1.6f;

            // Top-left info panel
            float panelW = fs * 26;
            GUI.Box(new Rect(pad, pad, panelW, lineH * 2.4f), GUIContent.none, panel);
            int secs = Mathf.Max(0, Mathf.CeilToInt(ticksLeft / (float)TicksPerSecond));
            GUI.Label(new Rect(pad * 2, pad * 1.3f, panelW, lineH), levelNumber > 0 ? $"Level {levelNumber}: {level.name}" : level.name, darkLabel);
            string secretText = secretsTotal > 0 ? $"   Secrets {secretsFound}/{secretsTotal}" : "";
            GUI.Label(new Rect(pad * 2, pad * 1.3f + lineH, panelW, lineH),
                $"Out {released}/{level.cats}   Home {saved} (need {level.need})   Lost {lost}   Time {secs / 60}:{secs % 60:00}{secretText}", darkSmall);

            // Top-right: speed and zoom (each labelled, with matching - / + buttons), restart, levels
            if (mode == GameMode.Playing)
            {
                float pw = fs * 13f, px = Screen.width - pad - pw;
                float rowH = lineH * 1.15f, sb = fs * 2.6f;
                GUI.Box(new Rect(px, pad, pw, rowH * 2f + pad * 1.2f), GUIContent.none, panel);
                var rowLabel = new GUIStyle(darkLabel) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(fs * 0.85f) };

                // Speed row
                float ry = pad + pad * 0.6f;
                GUI.Label(new Rect(px + pad, ry, pw - sb * 2 - pad * 2, rowH), $"Speed {Mathf.RoundToInt(speedPercent)}%", rowLabel);
                GUI.enabled = speedPercent > minSpeedPercent;
                if (GUI.Button(new Rect(px + pw - pad - sb * 2 - pad * 0.4f, ry + 1, sb, rowH - 2), "-", button))
                    speedPercent = Mathf.Max(minSpeedPercent, speedPercent - 10f);
                GUI.enabled = speedPercent < 100f;
                if (GUI.Button(new Rect(px + pw - pad - sb, ry + 1, sb, rowH - 2), "+", button))
                    speedPercent = Mathf.Min(100f, speedPercent + 10f);
                GUI.enabled = true;

                // Zoom row
                ry += rowH;
                int zoomPct = Mathf.RoundToInt(MaxView / viewH * 100f);
                GUI.Label(new Rect(px + pad, ry, pw - sb * 2 - pad * 2, rowH), $"Zoom {zoomPct}%", rowLabel);
                if (GUI.Button(new Rect(px + pw - pad - sb * 2 - pad * 0.4f, ry + 1, sb, rowH - 2), "-", button))
                    ZoomAt(1.25f, new Vector2(Screen.width / 2f, Screen.height / 2f));
                if (GUI.Button(new Rect(px + pw - pad - sb, ry + 1, sb, rowH - 2), "+", button))
                    ZoomAt(0.8f, new Vector2(Screen.width / 2f, Screen.height / 2f));

                float bw = fs * 7f, by = pad * 2.6f + rowH * 2f;
                if (GUI.Button(new Rect(Screen.width - pad - bw, by, bw, lineH), "Restart", button))
                {
                    BuildLevel(false);
                    return;
                }
                if (GUI.Button(new Rect(Screen.width - pad - bw, by + lineH * 1.2f, bw, lineH), "Levels", button))
                {
                    ClearLevel();
                    mode = GameMode.Menu;
                    return;
                }
            }

            DrawMinimap(pad);
            DrawPopups();

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

            HandleCameraMouse();

            // Highlight the cat under the mouse, and give it the trait on click.
            Event e = Event.current;
            Cat hovered = CatUnderMouse(e.mousePosition);
            if (hovered != null) DrawOutline(hovered, Color.white);
            if (e.type == EventType.MouseDown && e.button == 0 && hovered != null)
            {
                TryAssign(hovered);
                e.Use();
            }
        }

        void DrawPopups()
        {
            float y = Screen.height * 0.24f;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - popups[i].time;
                if (age > 3f) { popups.RemoveAt(i); continue; }
            }
            foreach (var p in popups)
            {
                float age = Time.unscaledTime - p.time;
                float a = Mathf.Clamp01(3f - age);
                var st = new GUIStyle(big) { fontSize = Mathf.RoundToInt(fs * 1.4f) };
                st.normal.textColor = new Color(1f, 0.92f, 0.5f, a);
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, a);
                ShadowLabel(new Rect(0, y - age * fs * 0.5f, Screen.width, fs * 2.2f), p.text, st);
                GUI.color = old;
                y += fs * 2.2f;
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
            var goals = Goals();
            var box = new Rect(Screen.width * 0.22f, Screen.height * 0.2f, Screen.width * 0.56f, Screen.height * 0.58f);
            GUI.Box(box, GUIContent.none, panel);

            string msg;
            if (won)
                msg = saved == level.cats ? $"PURR-FECT!\nAll {saved} kittens made it home!" : $"Level complete!\n{saved} kittens home (needed {level.need})";
            else
                msg = (timeUp ? "Out of time!\n" : "") + $"Only {saved} kittens made it home...\nYou need {level.need}.";
            var bigDark = new GUIStyle(big); bigDark.normal.textColor = InkCol;
            GUI.Label(new Rect(box.x, box.y + fs * 0.5f, box.width, fs * 5f), msg, bigDark);

            // Goals with ticks
            float gy = box.y + fs * 6f, gx = box.x + box.width * 0.18f, lineH = fs * 1.6f;
            for (int i = 0; i < goals.Count; i++)
            {
                DrawTick(new Rect(gx, gy + fs * 0.2f, fs * 1.2f, fs * 1.2f), goals[i].done);
                GUI.Label(new Rect(gx + fs * 1.8f, gy, box.width * 0.7f, lineH), goals[i].text, darkLabel);
                gy += lineH;
            }

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
            if (GUI.Button(new Rect(bx, by, bw, bh), won ? "Play again" : "Try again", button)) { BuildLevel(false); return; }
            bx += bw + fs;
            if (GUI.Button(new Rect(bx, by, bw, bh), "Levels", button)) { ClearLevel(); mode = GameMode.Menu; }
        }
    }
}
