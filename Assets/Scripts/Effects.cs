using UnityEngine;

namespace Cattas
{
    // Makes a sprite fade out (and optionally float up / grow), then deletes it.
    // Used for quick visual effects like the ZAP flash.
    public class FadeAway : MonoBehaviour
    {
        public float life = 0.5f;   // seconds
        public float rise = 0f;     // units per second upwards
        public float grow = 0f;     // extra scale by the end (0.5 = 50% bigger)

        float t;
        SpriteRenderer sr;
        Color startColour;
        Vector3 startScale;

        void Start()
        {
            sr = GetComponent<SpriteRenderer>();
            startColour = sr.color;
            startScale = transform.localScale;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = t / life;
            if (k >= 1f) { Destroy(gameObject); return; }
            sr.color = new Color(startColour.r, startColour.g, startColour.b, startColour.a * (1f - k));
            transform.localPosition += Vector3.up * rise * Time.deltaTime;
            transform.localScale = startScale * (1f + grow * k);
        }
    }
}
