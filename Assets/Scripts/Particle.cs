using UnityEngine;

namespace Cattas
{
    // A tiny bit of dust, debris or sparkle that flies, falls and fades.
    public class Particle : MonoBehaviour
    {
        public Vector2 velocity;
        public float gravity = -220f;
        public float life = 0.6f;
        public float spin;

        float t;
        SpriteRenderer sr;
        Color startColour;

        public static int Alive;

        void Start()
        {
            sr = GetComponent<SpriteRenderer>();
            startColour = sr.color;
            Alive++;
        }

        void OnDestroy() { Alive--; }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            if (t >= life) { Destroy(gameObject); return; }
            velocity.y += gravity * dt;
            transform.localPosition += (Vector3)(velocity * dt);
            if (spin != 0f) transform.Rotate(0f, 0f, spin * dt);
            var c = startColour;
            c.a = startColour.a * (1f - t / life);
            sr.color = c;
        }
    }
}
