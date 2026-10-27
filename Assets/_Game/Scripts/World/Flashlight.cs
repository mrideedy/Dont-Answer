using UnityEngine;

namespace DontAnswer
{
    /// <summary>Marco's flashlight - or, without it, your phone's weak little torch. F toggles.</summary>
    public class Flashlight : MonoBehaviour
    {
        public Light beam;
        public float flashlightIntensity = 7f, flashlightRange = 15f, flashlightAngle = 54f;
        public float phoneIntensity = 1.8f, phoneRange = 6f, phoneAngle = 72f;

        public bool HasFlashlight { get; set; }
        public bool On { get; private set; }
        public float Range => beam != null ? beam.range : 0f;

        void Awake()
        {
            if (beam != null) beam.enabled = false;
        }

        public void Set(bool on)
        {
            On = on;
            if (beam == null) return;
            beam.enabled = on;
            beam.intensity = HasFlashlight ? flashlightIntensity : phoneIntensity;
            beam.range = HasFlashlight ? flashlightRange : phoneRange;
            beam.spotAngle = HasFlashlight ? flashlightAngle : phoneAngle;
            beam.innerSpotAngle = beam.spotAngle * 0.55f;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx("flashlight", transform.position, 0.6f);
        }

        public void Toggle() => Set(!On);

        void Update()
        {
            if (On && beam != null)
                beam.intensity = (HasFlashlight ? flashlightIntensity : phoneIntensity) * (0.96f + Mathf.PerlinNoise(Time.time * 7f, 0f) * 0.08f);
        }
    }
}
