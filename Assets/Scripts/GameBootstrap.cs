using System.Collections.Generic;
using UnityEngine;

namespace Cattas
{
    // The "brain" of the game. It starts itself automatically when you press Play,
    // so there is nothing to set up in the Unity editor.
    public class GameBootstrap : MonoBehaviour
    {
        // ---------- Level settings (try changing these numbers!) ----------
        public const int LevelWidth = 640;   // level size in pixels
        public const int LevelHeight = 360;
        public int totalCats = 10;           // how many cats come out of the hatch
        public int catsNeeded = 7;           // how many must reach the exit to win
        public float secondsBetweenCats = 1.5f;

        const int TicksPerSecond = 30;       // game logic runs at a steady 30 steps a second

        Camera cam;
        Transform levelRoot;
        DiggableGround ground;
        Sprite catSprite;
        readonly List<Cat> cats = new List<Cat>();
        Vector2Int spawnPoint;
        RectInt exitZone;

        readonly int[] traitsLeft = new int[CatTraits.Count];
        CatTrait selectedTrait = CatTrait.Scratcher;

        int released, saved, lost, ticksUntilNextCat;
        bool fastForward;
        float tickTimer;

        static readonly Color[] CatColours =
        {
            new Color(1f, 0.62f, 0.25f),   // ginger
            new Color(0.62f, 0.62f, 0.68f), // grey
            new Color(1f, 0.93f, 0.78f),   // cream
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
            BuildLevel();
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
            cam.orthographicSize = LevelHeight / 2f;
            cam.transform.position = new Vector3(LevelWidth / 2f, LevelHeight / 2f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.76f, 0.95f); // sky blue
        }

        // ---------- Level 1 (test level) ----------
        void BuildLevel()
        {
            if (levelRoot != null) Destroy(levelRoot.gameObject);
            cats.Clear();
            released = saved = lost = 0;
            ticksUntilNextCat = TicksPerSecond; // first cat after 1 second

            levelRoot = new GameObject("Level").transform;
            levelRoot.SetParent(transform, false);

            var groundObj = new GameObject("Ground");
            groundObj.transform.SetParent(levelRoot, false);
            ground = groundObj.AddComponent<DiggableGround>();
            ground.Init(LevelWidth, LevelHeight);

            ground.FillRect(0, 160, 640, 190);    // main floor (top surface at y=190)
            ground.FillRect(30, 225, 260, 245);   // upper ledge the cats start on
            ground.FillHill(330, 190, 30, 12);    // little hill to walk over
            ground.FillRect(450, 190, 470, 300);  // tall wall blocking the way
            ground.FillRect(0, 100, 640, 140);    // lower floor, where the exit is
            ground.FinishPainting();

            // How many of each trait the player gets on this level
            for (int i = 0; i < traitsLeft.Length; i++) traitsLeft[i] = 0;
            traitsLeft[(int)CatTrait.Scratcher] = 3;
            selectedTrait = CatTrait.Scratcher;

            // Hatch where cats appear
            spawnPoint = new Vector2Int(70, 272);
            MakeBox("Hatch", new Vector2Int(58, 274), 24, 10, new Color32(70, 60, 60, 255), 1);

            // Exit (a little cat-flap house) on the LOWER floor - dig down to reach it!
            MakeBox("Exit House", new Vector2Int(550, 140), 20, 26, new Color32(150, 90, 50, 255), 1);
            MakeBox("Exit Door", new Vector2Int(555, 140), 10, 14, new Color32(40, 25, 20, 255), 2);
            exitZone = new RectInt(556, 140, 9, 10);
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

        // ---------- Game loop ----------
        void Update()
        {
            tickTimer += Time.deltaTime * (fastForward ? 3f : 1f);
            float tickLength = 1f / TicksPerSecond;
            int safety = 0;
            while (tickTimer >= tickLength && safety++ < 20)
            {
                tickTimer -= tickLength;
                GameTick();
            }
            if (safety >= 20) tickTimer = 0f;
        }

        void GameTick()
        {
            if (released < totalCats && --ticksUntilNextCat <= 0)
            {
                SpawnCat();
                ticksUntilNextCat = Mathf.RoundToInt(secondsBetweenCats * TicksPerSecond);
            }
            for (int i = cats.Count - 1; i >= 0; i--)
                cats[i].Tick();
        }

        void SpawnCat()
        {
            var go = new GameObject("Cat " + (released + 1));
            go.transform.SetParent(levelRoot, false);
            var cat = go.AddComponent<Cat>();
            cat.Init(this, ground, spawnPoint.x, spawnPoint.y, catSprite, CatColours[released % CatColours.Length]);
            cats.Add(cat);
            released++;
        }

        public bool IsAtExit(int x, int y) => exitZone.Contains(new Vector2Int(x, y));
        public void OnCatSaved(Cat cat) { cats.Remove(cat); saved++; }
        public void OnCatLost(Cat cat) { cats.Remove(cat); lost++; }
        bool IsFinished => released == totalCats && cats.Count == 0;

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
        void DrawTraitBar(int fs, float pad)
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
                bool built = CatTraits.IsImplemented(trait);
                string label = built
                    ? $"{CatTraits.DisplayName(trait)}\n{traitsLeft[i]}"
                    : $"{CatTraits.DisplayName(trait)}\n(soon)";
                GUI.enabled = built;
                GUI.backgroundColor = (built && trait == selectedTrait) ? new Color(1f, 0.8f, 0.3f) : oldBg;
                if (GUI.Button(new Rect(x0 + i * (bw + gap), y0, bw, bh), label, style))
                    selectedTrait = trait;
            }
            GUI.enabled = oldEnabled;
            GUI.backgroundColor = oldBg;
        }

        void OnGUI()
        {
            int fs = Mathf.Max(14, Screen.height / 36);
            var label = new GUIStyle(GUI.skin.label) { fontSize = fs, fontStyle = FontStyle.Bold };
            label.normal.textColor = Color.white;
            var small = new GUIStyle(label) { fontSize = Mathf.RoundToInt(fs * 0.8f), fontStyle = FontStyle.Normal };
            var button = new GUIStyle(GUI.skin.button) { fontSize = fs };
            float pad = fs * 0.6f, lineH = fs * 1.6f;

            GUI.Box(new Rect(pad, pad, fs * 28, lineH * 2.2f), GUIContent.none);
            GUI.Label(new Rect(pad * 2, pad, fs * 28, lineH),
                $"Cats out: {released}/{totalCats}    Saved: {saved}    Lost: {lost}", label);
            GUI.Label(new Rect(pad * 2, pad + lineH, fs * 28, lineH),
                $"Save {catsNeeded} to win!  Pick a trait below, then click a cat.", small);

            float bw = fs * 7;
            if (GUI.Button(new Rect(Screen.width - pad - bw, pad, bw, lineH), fastForward ? "Speed: 3x" : "Speed: 1x", button))
                fastForward = !fastForward;
            if (GUI.Button(new Rect(Screen.width - pad - bw, pad + lineH * 1.2f, bw, lineH), "Restart", button))
                BuildLevel();

            if (IsFinished)
            {
                var big = new GUIStyle(label) { fontSize = fs * 2, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                var box = new Rect(Screen.width * 0.2f, Screen.height * 0.3f, Screen.width * 0.6f, Screen.height * 0.4f);
                GUI.Box(box, GUIContent.none);
                GUI.Box(box, GUIContent.none);
                string msg = saved >= catsNeeded
                    ? $"Purr-fect! You saved {saved} cats!"
                    : $"Only {saved} cats made it home...\nYou need {catsNeeded}.";
                GUI.Label(new Rect(box.x, box.y, box.width, box.height * 0.7f), msg, big);
                if (GUI.Button(new Rect(box.center.x - bw, box.yMax - lineH * 1.6f, bw * 2, lineH * 1.2f), "Play again", button))
                    BuildLevel();
                return;
            }

            DrawTraitBar(fs, pad);

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
    }
}
