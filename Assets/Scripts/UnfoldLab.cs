using System.Collections.Generic;
using UnityEngine;

namespace Cattas
{
    // U LAB - the doll-house idea.
    //
    // The artist paints ONE long flat picture: left wall | back wall | right wall, each seen head-on, joined at two corner columns.
    // The game can show it two ways, and fold between them:
    //   FLAT    the long strip, scrolled and played head-on (side-view kittens, easy art)
    //   U       the same three pictures hinged at the corner columns into a real U-shaped room seen in perspective (a doll house)
    //
    // The left and right walls are hinged: Fold 0 = laid flat in line with the back wall, Fold 1 = swung 90 degrees towards the viewer.
    // Kittens live on ONE straight line along the strip (s = 0 .. picture width) and are carried round the hinges, so a kitten
    // that walks off the end of the left wall simply turns the corner onto the back wall and on to the right wall.
    // The picture itself is never changed: the three walls are plain slices of it.
    public class UnfoldLab : MonoBehaviour
    {
        // measured from the picture (pixels): the corner columns' centres, the wall/floor line (from the top) and the picture size
        const float PicW = 2000f, PicH = 667f, Fold1 = 625f, Fold2 = 1367f, FloorY = 563f;
        const float KittenScale = 4.2f, KittenDepth = 22f;        // kitten drawing size; how far in front of the wall it walks
        const int OrderFloor = -12, OrderBand = -11, OrderWall = -10, OrderKitten = 5;
        const float Fov = 32f, FovU = 58f;      // flat strip = long lens; folded room = wide lens (doll-house look)

        Camera cam;
        CatRig rig;
        System.Action onExit;
        Transform root, leftPivot, rightPivot;
        readonly List<Texture2D> textures = new List<Texture2D>();
        readonly List<Material> bandMaterials = new List<Material>();
        Material floorMaterial;
        Mesh floorMesh;
        float fold, foldTarget;                   // 0 = flat strip, 1 = U
        float yaw, pitch = 10f, zoom = 1f;
        Vector3 pan;
        bool dragOrbit, dragPan; Vector2 lastMouse;
        bool paused;
        float timeScale = 1f;
        Rect panelRect;

        float BackW => Fold2 - Fold1;
        float LeftW => Fold1;
        float RightW => PicW - Fold2;
        float WallH => FloorY;
        float BandH => PicH - FloorY;

        class Walker { public Transform wrapper; public CatPuppet puppet; public float s, speed; public int dir = 1; public float lastScreenX; public bool flip; }
        readonly List<Walker> walkers = new List<Walker>();

        public void Init(Camera camera, CatRig kittenRig, System.Action exit)
        {
            cam = camera; rig = kittenRig; onExit = exit;
            var pano = Art.Load("ushape_pano");
            if (pano == null) { Debug.LogError("Cattas: U Lab picture missing"); Exit(); return; }
            textures.Add(pano);
            Build(pano);
            cam.orthographic = false;
            cam.fieldOfView = Fov;
            cam.nearClipPlane = 5f;
            cam.farClipPlane = 20000f;
            cam.backgroundColor = new Color(0.1f, 0.07f, 0.06f);
            for (int i = 0; i < 7; i++) SpawnKitten(60f + i * 280f, i);
            PlaceCamera();
        }

        // ---------- the three walls ----------
        static Mesh Quad(float x0, float y0, float x1, float y1, float u0, float v0, float u1, float v1)
        {
            var m = new Mesh();
            m.vertices = new[] { new Vector3(x0, y0, 0), new Vector3(x0, y1, 0), new Vector3(x1, y1, 0), new Vector3(x1, y0, 0) };
            m.uv = new[] { new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0) };
            m.colors32 = new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        MeshRenderer MakeMesh(string name, Transform parent, Mesh mesh, Material mat, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = order;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        Material PanoMaterial(Texture2D tex)
        {
            var m = new Material(GameBootstrap.SpriteMaterial) { mainTexture = tex };
            return m;
        }

        Material BandMat(Texture2D pano) { var m = PanoMaterial(pano); bandMaterials.Add(m); return m; }

        void Build(Texture2D pano)
        {
            root = new GameObject("U Lab").transform;
            root.SetParent(transform, false);
            leftPivot = new GameObject("Left hinge").transform; leftPivot.SetParent(root, false); leftPivot.localPosition = Vector3.zero;
            rightPivot = new GameObject("Right hinge").transform; rightPivot.SetParent(root, false); rightPivot.localPosition = new Vector3(BackW, 0f, 0f);

            float vFloor = 1f - FloorY / PicH;
            float uL = 0f, uB1 = Fold1 / PicW, uB2 = Fold2 / PicW, uR = 1f;
            var wallMat = PanoMaterial(pano);
            // walls (above the floor line). The back wall sits on x = 0 .. BackW; the side walls hang off the hinges.
            MakeMesh("Back wall", root, Quad(0, 0, BackW, WallH, uB1, vFloor, uB2, 1f), wallMat, OrderWall);
            MakeMesh("Left wall", leftPivot, Quad(-LeftW, 0, 0, WallH, uL, vFloor, uB1, 1f), wallMat, OrderWall);
            MakeMesh("Right wall", rightPivot, Quad(0, 0, RightW, WallH, uB2, vFloor, uR, 1f), wallMat, OrderWall);
            // the strip of floor painted under each wall: only used while the strip is flat, fades away as it folds
            MakeMesh("Back floor strip", root, Quad(0, -BandH, BackW, 0, uB1, 0f, uB2, vFloor), BandMat(pano), OrderBand);
            MakeMesh("Left floor strip", leftPivot, Quad(-LeftW, -BandH, 0, 0, uL, 0f, uB1, vFloor), BandMat(pano), OrderBand);
            MakeMesh("Right floor strip", rightPivot, Quad(0, -BandH, RightW, 0, uB2, 0f, uR, vFloor), BandMat(pano), OrderBand);

            // the real floor of the U room (placeholder pink tiles until the artist paints one)
            var floorTex = MakeFloorTexture();
            textures.Add(floorTex);
            floorMaterial = new Material(GameBootstrap.SpriteMaterial) { mainTexture = floorTex };
            float depth = (LeftW + RightW) * 0.5f;
            floorMesh = new Mesh();
            floorMesh.vertices = new[] { new Vector3(0, 0, 0), new Vector3(BackW, 0, 0), new Vector3(BackW, 0, -depth), new Vector3(0, 0, -depth) };
            floorMesh.uv = new[] { new Vector2(0, 0), new Vector2(BackW / 100f, 0), new Vector2(BackW / 100f, depth / 100f), new Vector2(0, depth / 100f) };
            floorMesh.colors32 = new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) };
            floorMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            floorMesh.RecalculateNormals();
            var floorGo = MakeMesh("Floor", root, floorMesh, floorMaterial, OrderFloor);
            floorGo.name = "Floor";
        }

        static Texture2D MakeFloorTexture()
        {
            const int N = 128;                                  // 4 x 4 tiles per repeat (each repeat = 100 picture pixels)
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            var tile = new Color(0.95f, 0.5f, 0.42f);
            var grout = new Color(1f, 0.72f, 0.62f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int tx = x % 32, ty = y % 32;
                    bool joint = tx < 1 || ty < 1;
                    float shade = 0.94f + 0.06f * Mathf.Sin((x / 32 + y / 32 * 3) * 1.7f);
                    var c = joint ? grout : tile * shade;
                    c.a = 1f;
                    px[y * N + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        // ---------- kittens ----------
        void SpawnKitten(float s, int n)
        {
            if (rig == null) return;
            var wrapper = new GameObject("Kitten").transform;
            wrapper.SetParent(root, false);
            var puppet = CatPuppet.Create(wrapper, rig, Color.white, OrderKitten);
            puppet.state = CatState.Walking;
            puppet.moveScale = 1f / KittenScale;
            var w = new Walker { wrapper = wrapper, puppet = puppet, s = s, speed = 85f * (1f + ((n * 37) % 11 - 5) * 0.012f), dir = n % 2 == 0 ? 1 : -1 };
            walkers.Add(w);
            Place(w);
        }

        // where does a spot s along the strip (and y up from the floor, d in front of the wall) sit in the room right now?
        Vector3 StripToWorld(float s, float y, float d)
        {
            float a = Mathf.SmoothStep(0f, 1f, fold) * 90f;
            if (s < Fold1)
            {
                var offset = new Vector3(-(Fold1 - s), y, -d);                    // flat position relative to the left hinge
                return leftPivot.position + Quaternion.AngleAxis(-a, Vector3.up) * offset;
            }
            if (s > Fold2)
            {
                var offset = new Vector3(s - Fold2, y, -d);
                return rightPivot.position + Quaternion.AngleAxis(a, Vector3.up) * offset;
            }
            return new Vector3(s - Fold1, y, -d);
        }

        void Place(Walker k)
        {
            Vector3 p = StripToWorld(k.s, 0f, KittenDepth);
            k.wrapper.position = p;
            k.wrapper.localScale = Vector3.one * KittenScale;
            k.wrapper.rotation = cam.transform.rotation;                           // paper-doll kittens always face the camera
            // face the way we are walking ON SCREEN
            float sx = cam.WorldToScreenPoint(p).x;
            if (Mathf.Abs(sx - k.lastScreenX) > 0.05f) k.flip = sx < k.lastScreenX;
            k.lastScreenX = sx;
            k.puppet.flip = k.flip;
        }

        // ---------- camera ----------
        void PlaceCamera()
        {
            float t = Mathf.SmoothStep(0f, 1f, fold);
            float aspect = Mathf.Max(0.5f, Screen.width / (float)Screen.height);
            cam.fieldOfView = Mathf.Lerp(Fov, FovU, t);
            float tanF = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad), tanU = Mathf.Tan(FovU * 0.5f * Mathf.Deg2Rad);
            float dFlat = (PicW * 0.52f) / (tanF * aspect);
            float dU = Mathf.Max(BackW / 0.52f / (2f * tanU * aspect), WallH / (2f * 0.9f * tanU)) - LeftW * 0.45f;     // back wall fills about half the screen
            float dist = Mathf.Lerp(dFlat, dU, t) * zoom;
            Vector3 flatTarget = new Vector3(BackW * 0.5f, WallH * 0.45f, 0f);
            Vector3 uTarget = new Vector3(BackW * 0.5f, WallH * 0.38f, -LeftW * 0.45f);
            Vector3 target = Vector3.Lerp(flatTarget, uTarget, t) + pan;
            float py = Mathf.Lerp(0f, pitch, t), yw = Mathf.Lerp(0f, yaw, t);
            Quaternion rot = Quaternion.Euler(py, yw, 0f);
            cam.transform.rotation = rot;
            cam.transform.position = target + rot * new Vector3(0f, 0f, -dist);
        }

        // ---------- loop ----------
        void Update()
        {
            if (cam == null || root == null) return;
            ReadInput();
            if (cam == null || root == null) return;
            float dt = Time.unscaledDeltaTime;
            fold = Mathf.MoveTowards(fold, foldTarget, dt / 2.2f);
            leftPivot.localRotation = Quaternion.AngleAxis(-Mathf.SmoothStep(0f, 1f, fold) * 90f, Vector3.up);
            rightPivot.localRotation = Quaternion.AngleAxis(Mathf.SmoothStep(0f, 1f, fold) * 90f, Vector3.up);
            float vis = Mathf.SmoothStep(0f, 1f, fold);
            float stripAlpha = Mathf.Clamp01(1f - vis * 3f);
            foreach (var m in bandMaterials) m.color = new Color(1f, 1f, 1f, stripAlpha);
            floorMaterial.color = new Color(1f, 1f, 1f, Mathf.Clamp01(vis * 1.6f - 0.3f));
            PlaceCamera();
            float step = paused ? 0f : dt * timeScale;
            foreach (var k in walkers)
            {
                k.s += k.dir * k.speed * step;
                if (k.s < 30f) { k.s = 30f; k.dir = 1; }
                else if (k.s > PicW - 30f) { k.s = PicW - 30f; k.dir = -1; }
                Place(k);
            }
        }

        void ReadInput()
        {
            bool escape = false, rightDown = false, middleDown = false, rightHeld = false, middleHeld = false;
            Vector2 mouse = Vector2.zero; float wheel = 0f;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var ms = UnityEngine.InputSystem.Mouse.current;
            if (kb != null) escape = kb.escapeKey.wasPressedThisFrame;
            if (ms != null)
            {
                mouse = ms.position.ReadValue(); wheel = ms.scroll.ReadValue().y / 120f;
                rightDown = ms.rightButton.wasPressedThisFrame; rightHeld = ms.rightButton.isPressed;
                middleDown = ms.middleButton.wasPressedThisFrame; middleHeld = ms.middleButton.isPressed;
            }
#else
            escape = Input.GetKeyDown(KeyCode.Escape);
            mouse = Input.mousePosition; wheel = Input.mouseScrollDelta.y;
            rightDown = Input.GetMouseButtonDown(1); rightHeld = Input.GetMouseButton(1);
            middleDown = Input.GetMouseButtonDown(2); middleHeld = Input.GetMouseButton(2);
#endif
            if (escape) { Exit(); return; }
            bool overPanel = panelRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
            if (!overPanel && !Mathf.Approximately(wheel, 0f)) zoom = Mathf.Clamp(zoom * Mathf.Pow(0.9f, wheel), 0.25f, 1.6f);
            if (rightDown && !overPanel) { dragOrbit = true; lastMouse = mouse; }
            if (middleDown && !overPanel) { dragPan = true; lastMouse = mouse; }
            if (!rightHeld) dragOrbit = false;
            if (!middleHeld) dragPan = false;
            Vector2 delta = mouse - lastMouse; lastMouse = mouse;
            if (dragOrbit) { yaw = Mathf.Clamp(yaw + delta.x * 0.25f, -70f, 70f); pitch = Mathf.Clamp(pitch - delta.y * 0.25f, -5f, 60f); }
            if (dragPan) pan += (-cam.transform.right * delta.x - cam.transform.up * delta.y) * (Mathf.Max(1f, zoom) * 1.2f);
        }

        // ---------- buttons ----------
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
            GUI.Label(new Rect(x, y, bw * 3f, bh), "U Lab - fold the room (Esc = menu)", labelStyle); y += bh;
            if (GUI.Button(new Rect(x, y, bw, bh), "Back to menu", buttonStyle)) { Exit(); return; } y += bh + m * 0.5f;
            if (GUI.Button(new Rect(x, y, bw * 1.3f, bh), foldTarget < 0.5f ? "FOLD into a U room" : "UNFOLD to the flat strip", buttonStyle)) foldTarget = foldTarget < 0.5f ? 1f : 0f; y += bh + m * 0.5f;
            if (GUI.Button(new Rect(x, y, bw, bh), paused ? "Resume" : "Pause", buttonStyle)) paused = !paused; y += bh + m * 0.5f;
            if (GUI.Button(new Rect(x, y, bw, bh), "Reset view", buttonStyle)) { yaw = 0f; pitch = 10f; zoom = 1f; pan = Vector3.zero; } y += bh + m * 0.5f;
            GUI.Label(new Rect(x, y, bw * 1.5f, bh), $"Speed x{timeScale:0.00}", labelStyle); y += bh * 0.8f;
            timeScale = GUI.HorizontalSlider(new Rect(x, y, bw * 1.4f, bh * 0.5f), timeScale, 0.1f, 3f); y += bh * 0.8f;
            GUI.Label(new Rect(x, y, bw * 2.4f, bh * 2.6f), "Wheel: zoom\nRight-drag: orbit (U view)\nMiddle-drag: move view", labelStyle); y += bh * 2.6f;
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
