using UnityEngine;

namespace DontAnswer
{
    /// <summary>
    /// The sky over Elm Street: a runtime copy of the panoramic night-sky material (so play never edits the asset),
    /// tinted for dusk and sunrise by the director and blown out white by the lightning.
    /// </summary>
    public class NightSky : MonoBehaviour
    {
        public static NightSky Instance { get; private set; }

        Material sky;
        Color baseTint, tint;
        float baseExposure, exposure, flash;

        void Awake()
        {
            Instance = this;
            if (RenderSettings.skybox == null) return;
            sky = new Material(RenderSettings.skybox) { name = "Night Sky (runtime)" };
            RenderSettings.skybox = sky;
            baseTint = tint = sky.HasProperty("_Tint") ? sky.GetColor("_Tint") : new Color(0.5f, 0.5f, 0.5f);
            baseExposure = exposure = sky.HasProperty("_Exposure") ? sky.GetFloat("_Exposure") : 1f;
        }

        /// <summary>Dusk, dawn: a tint (0.5 grey is neutral) and a brightness.</summary>
        public void SetMood(Color skyTint, float skyExposure)
        {
            tint = skyTint;
            exposure = skyExposure;
            Apply();
        }

        public void ResetMood() => SetMood(baseTint, baseExposure);

        /// <summary>Lightning: 0 = none, 1 = the clouds light up from inside.</summary>
        public void Flash(float amount)
        {
            flash = Mathf.Clamp01(amount);
            Apply();
        }

        void Apply()
        {
            if (sky == null) return;
            sky.SetColor("_Tint", Color.Lerp(tint, new Color(0.62f, 0.66f, 0.75f), flash));
            sky.SetFloat("_Exposure", Mathf.Lerp(exposure, 9f, flash));
        }

        void OnDestroy()
        {
            if (sky != null) Destroy(sky);
        }
    }
}
