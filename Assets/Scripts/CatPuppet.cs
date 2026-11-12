using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cattas
{
    // The "paper doll" kitten: separate pictures (body, head, tail, 4 legs) joined at pivots,
    // and animated by code - walking, galloping, climbing, digging, kneading, falling...
    //
    // The pieces and joints come from Assets/Resources/Art/<skin>_rig.txt (see tools/make_cat_parts.py).
    // A new skin (dinosaur, dragon...) is just new pictures with the same joints.
    public class CatRig
    {
        public class Part
        {
            public string name;
            public Sprite sprite;
            public Vector2 position;   // where the joint sits, in level pixels from the feet (facing right)
            public int order;          // 0 = furthest back
            public float shade = 1f;   // far-side legs are a little darker
        }

        public readonly List<Part> parts = new List<Part>();

        public static CatRig Load(string skin)
        {
            var file = Resources.Load<TextAsset>($"Art/{skin}_rig");
            if (file == null) return null;
            var rig = new CatRig();
            float ppu = 12.8f;
            var textures = new Dictionary<string, Texture2D>();
            foreach (var raw in file.text.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//")) continue;
                var p = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (p[0] == "ppu" && p.Length > 1) { ppu = F(p[1]); continue; }
                if (p[0] != "part" || p.Length < 9) continue;
                if (!textures.TryGetValue(p[2], out var tex))
                {
                    tex = Art.Load(p[2]);
                    textures[p[2]] = tex;
                }
                if (tex == null) return null; // missing picture - fall back to the plain kitten
                var pivot = new Vector2(F(p[3]) / tex.width, F(p[4]) / tex.height);
                rig.parts.Add(new Part
                {
                    name = p[1],
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu),
                    position = new Vector2(F(p[5]), F(p[6])),
                    order = int.Parse(p[7], CultureInfo.InvariantCulture),
                    shade = F(p[8]),
                });
            }
            return rig.parts.Count > 0 ? rig : null;
        }

        static float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    public class CatPuppet : MonoBehaviour
    {
        // Set by the Cat every game step
        [HideInInspector] public CatState state = CatState.Falling;
        [HideInInspector] public bool flip, floating, hacking;

        Transform rigRoot;                       // tilts and bobs the whole body
        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        readonly Dictionary<string, Vector3> rest = new Dictionary<string, Vector3>();
        readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        readonly List<float> shades = new List<float>();
        Color tint = Color.white;
        float phase, hackAmount, seed;
        Vector3 lastPos;
        bool started;

        public static CatPuppet Create(Transform parent, CatRig rig, Color tint, int sortingOrder)
        {
            var go = new GameObject("Kitten Puppet");
            go.transform.SetParent(parent, false);
            var group = go.AddComponent<SortingGroup>();   // keeps the kitten's pieces together in front/behind other things
            group.sortingOrder = sortingOrder;
            var puppet = go.AddComponent<CatPuppet>();
            puppet.Build(rig, tint);
            return puppet;
        }

        void Build(CatRig rig, Color startTint)
        {
            rigRoot = new GameObject("Rig").transform;
            rigRoot.SetParent(transform, false);
            foreach (var part in rig.parts)
            {
                var b = new GameObject(part.name).transform;
                b.SetParent(rigRoot, false);
                b.localPosition = part.position;
                var sr = b.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = part.sprite;
                sr.material = GameBootstrap.SpriteMaterial;
                sr.sortingOrder = part.order;
                renderers.Add(sr);
                shades.Add(part.shade);
                bones[part.name] = b;
                rest[part.name] = part.position;
            }
            seed = Random.Range(0f, 100f);
            SetColor(startTint);
        }

        public void SetColor(Color c)
        {
            tint = c;
            for (int i = 0; i < renderers.Count; i++)
            {
                float s = shades[i];
                renderers[i].color = new Color(c.r * s, c.g * s, c.b * s, c.a);
            }
        }

        // Fade every piece (used by the "heading home" ghost kittens)
        public void SetAlpha(float a)
        {
            var c = tint; c.a = a;
            SetColor(c);
        }

        void Rot(string bone, float degrees)
        {
            if (bones.TryGetValue(bone, out var b)) b.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        void Offset(string bone, Vector2 offset)
        {
            if (bones.TryGetValue(bone, out var b)) b.localPosition = rest[bone] + (Vector3)offset;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime, t = Time.time + seed;
            Vector3 wp = transform.position;
            if (!started) { lastPos = wp; started = true; }
            float moved = (wp - lastPos).magnitude;
            lastPos = wp;
            if (moved > 4f) moved = 0f; // a jump (e.g. starting to climb) isn't a step

            hackAmount = Mathf.MoveTowards(hackAmount, hacking ? 1f : 0f, dt * 10f);

            float amp = 0f, bob = 0f, tilt = 0f, head = 0f;
            float tail = Mathf.Sin(t * 3f) * 7f;
            float backFar = 0f, frontFar = 0f, backNear = 0f, frontNear = 0f;
            Vector2 frontNearOff = Vector2.zero, frontFarOff = Vector2.zero;
            bool gait = false;

            switch (state)
            {
                case CatState.Walking:
                    phase += moved * 0.55f; amp = 26f; gait = true;
                    bob = 0.22f * Mathf.Abs(Mathf.Sin(phase));
                    head = Mathf.Sin(phase * 2f) * 3f;
                    tail += Mathf.Sin(phase) * 6f;
                    break;
                case CatState.Zooming:
                    phase += dt * 24f; amp = 42f; gait = true;
                    tilt = -7f; bob = 0.45f * Mathf.Abs(Mathf.Sin(phase));
                    tail = -30f + Mathf.Sin(t * 20f) * 5f; head = -4f;
                    break;
                case CatState.Climbing:
                    phase += moved * 0.9f; amp = 38f; gait = true;
                    tail = Mathf.Sin(t * 5f) * 12f;
                    break;
                case CatState.Building:
                    phase += moved * 0.55f; amp = moved > 0f ? 20f : 0f; gait = true;
                    head = -hackAmount * 24f; tilt = -hackAmount * 5f; tail += hackAmount * 15f;
                    break;
                case CatState.Digging:
                case CatState.Scratching:
                {
                    float speed = state == CatState.Digging ? 16f : 12f;
                    tilt = state == CatState.Digging ? -16f : -10f;
                    frontNear = Mathf.Sin(t * speed) * 38f - 15f;
                    frontFar = Mathf.Sin(t * speed + Mathf.PI) * 38f - 15f;
                    backNear = 8f; backFar = 8f;
                    head = -10f; tail = 20f + Mathf.Sin(t * 6f) * 10f;
                    break;
                }
                case CatState.Blocking:
                {
                    // sitting up, kneading biscuits: left paw, right paw, left paw...
                    tilt = 12f;
                    backNear = 62f; backFar = 62f;
                    float k = t * 7f;
                    frontNearOff = new Vector2(0f, Mathf.Max(0f, Mathf.Sin(k)) * 0.9f);
                    frontFarOff = new Vector2(0f, Mathf.Max(0f, Mathf.Sin(k + Mathf.PI)) * 0.9f);
                    frontNear = -6f; frontFar = -6f;
                    head = Mathf.Sin(k) * 3f + 4f;
                    tail = 10f + Mathf.Sin(t * 2f) * 15f;
                    break;
                }
                case CatState.Falling:
                    if (floating)
                    {
                        float sway = Mathf.Sin(t * 4f) * 8f;
                        frontNear = frontFar = 45f + sway; backNear = backFar = -45f - sway;
                        tail = 35f; head = 6f;
                    }
                    else
                    {
                        frontNear = frontFar = 30f; backNear = backFar = -30f;
                        tail = 35f + Mathf.Sin(t * 14f) * 10f; head = 8f;
                    }
                    break;
                case CatState.Splatted:
                    frontNear = frontFar = 80f; backNear = backFar = -80f; tail = -10f;
                    break;
            }

            if (gait)
            {
                // diagonal pairs move together, like a real trotting cat
                backFar = Mathf.Sin(phase) * amp;
                frontNear = Mathf.Sin(phase) * amp;
                backNear = Mathf.Sin(phase + Mathf.PI) * amp;
                frontFar = Mathf.Sin(phase + Mathf.PI) * amp;
            }

            transform.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
            rigRoot.localRotation = Quaternion.Euler(0f, 0f, tilt);
            rigRoot.localPosition = new Vector3(0f, bob, 0f);
            Rot("leg_back_far", backFar);
            Rot("leg_front_far", frontFar);
            Rot("leg_back_near", backNear);
            Rot("leg_front_near", frontNear);
            Offset("leg_front_near", frontNearOff);
            Offset("leg_front_far", frontFarOff);
            Rot("tail", tail);
            Rot("head", head);
        }
    }

    // Moves the ghost kitten up and removes it when done
    public class FadeGroupMover : MonoBehaviour
    {
        public float life = 1f, rise = 40f;
        float t;
        void Update()
        {
            t += Time.deltaTime;
            if (t >= life) { Destroy(gameObject); return; }
            transform.localPosition += Vector3.up * rise * Time.deltaTime;
        }
    }

    // Fades out a whole kitten puppet (all its pieces), drifting up - for kittens heading home to recover.
    public class FadeGroup : MonoBehaviour
    {
        public float life = 1f, rise = 40f;
        float t;
        CatPuppet puppet;

        void Start() { puppet = GetComponent<CatPuppet>(); }

        void Update()
        {
            t += Time.deltaTime;
            float k = t / life;
            if (k >= 1f) { Destroy(gameObject); return; }
            transform.localPosition += Vector3.up * rise * Time.deltaTime;
            if (puppet != null) puppet.SetAlpha(1f - k);
        }
    }
}
