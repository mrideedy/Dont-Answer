using UnityEngine;

namespace DontAnswer
{
    /// <summary>The porch light. Each visitor sets its mode - shadows and breath can only be checked while it's lit.</summary>
    public class PorchLight : MonoBehaviour
    {
        public Light lamp;
        public Renderer bulb;
        public float intensity = 6f;
        public Color bulbColor = new Color(1f, 0.8f, 0.55f);

        public PorchLightMode Mode { get; private set; } = PorchLightMode.On;
        public bool Powered { get; private set; } = true;
        public bool IsLit => lamp != null && lamp.enabled && lamp.intensity > intensity * 0.3f;

        Material bulbMat;
        float flickerTimer;
        bool flickerOn = true;

        void Awake()
        {
            if (bulb != null) bulbMat = bulb.material;
        }

        public void SetMode(PorchLightMode mode)
        {
            Mode = mode;
            flickerTimer = 0f;
            flickerOn = true;
        }

        public void SetPowered(bool on) => Powered = on;

        void Update()
        {
            float level;
            if (!Powered) level = 0f;
            else switch (Mode)
            {
                case PorchLightMode.On:
                    level = 1f + (Mathf.PerlinNoise(Time.time * 4f, 0.3f) - 0.5f) * 0.08f;
                    break;
                case PorchLightMode.Off:
                    level = 0f;
                    break;
                default:
                    flickerTimer -= Time.deltaTime;
                    if (flickerTimer <= 0f)
                    {
                        flickerOn = !flickerOn;
                        flickerTimer = flickerOn ? Random.Range(0.35f, 2.2f) : Random.Range(0.04f, 0.45f);
                        if (!flickerOn && Random.value < 0.4f && AudioManager.Instance != null)
                            AudioManager.Instance.PlaySfx("light_buzz", transform.position, 0.35f, Random.Range(0.9f, 1.15f));
                    }
                    level = flickerOn ? 1f : 0.02f;
                    break;
            }

            lamp.intensity = intensity * level;
            lamp.enabled = level > 0.01f;
            if (bulbMat != null) bulbMat.SetColor("_BaseColor", bulbColor * Mathf.Lerp(0.06f, 2.2f, level));
        }
    }
}
