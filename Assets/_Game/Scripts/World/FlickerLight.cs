using UnityEngine;

namespace DontAnswer
{
    /// <summary>Gentle candle/lamp flicker, plus a violent burst for scares.</summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        public float noise = 0.06f;
        public float speed = 2f;
        public Renderer emissive;
        public Color emissiveColor = Color.white;

        Light lamp;
        float baseIntensity, seed, burst;
        Material emissiveMat;

        public bool ForcedOff { get; set; }

        void Awake()
        {
            lamp = GetComponent<Light>();
            baseIntensity = lamp.intensity;
            seed = Random.value * 50f;
            if (emissive != null) emissiveMat = emissive.material;
        }

        public void Burst(float duration) => burst = Mathf.Max(burst, duration);

        public void SetBaseIntensity(float value) => baseIntensity = value;

        void Update()
        {
            float k = 1f + (Mathf.PerlinNoise(Time.time * speed, seed) - 0.5f) * 2f * noise;
            if (burst > 0f)
            {
                burst -= Time.deltaTime;
                k *= Random.value < 0.45f ? 0.03f : Random.Range(0.5f, 1.3f);
            }
            if (ForcedOff) k = 0f;
            lamp.intensity = baseIntensity * k;
            if (emissiveMat != null) emissiveMat.SetColor("_BaseColor", emissiveColor * Mathf.Max(0.05f, k));
        }
    }
}
