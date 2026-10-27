using System.Collections;
using UnityEngine;

namespace DontAnswer
{
    /// <summary>
    /// The storm. A strike drives the moon (the shadow-casting main light) hard so window frames throw shadows
    /// across the floor, lights the outside world, and blows the fog out to a pale sky for a heartbeat -
    /// long enough to see the tree line, the fence, and whatever is standing in the yard. Thunder follows.
    /// </summary>
    public class LightningSystem : MonoBehaviour
    {
        public static LightningSystem Instance { get; private set; }

        public Light moon;
        public Light outsideFlash;
        public Camera mainCamera;
        public Vector2 randomInterval = new Vector2(38f, 85f);
        public Color flashSky = new Color(0.2f, 0.23f, 0.3f);
        public Color flashMoon = new Color(0.78f, 0.84f, 1f);

        public bool Flashing { get; private set; }
        float timer;

        void Awake()
        {
            Instance = this;
            timer = Random.Range(randomInterval.x, randomInterval.y);
        }

        void Update()
        {
            var gd = GameDirector.Instance;
            if (gd == null || gd.State != GameState.Playing || gd.InCutscene || Flashing) return;
            if (gd.Minute >= StoryScript.Sunrise - 4) return;   // the storm passes before dawn
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Random.Range(randomInterval.x, randomInterval.y);
            StartCoroutine(Strike(Random.value < 0.35f, true));
        }

        /// <summary>One strike. Close strikes are brighter and the thunder comes almost at once.</summary>
        public IEnumerator Strike(bool close, bool thunder)
        {
            if (Flashing) yield break;
            Flashing = true;
            float moonBase = moon != null ? moon.intensity : 0f;
            Color moonColor = moon != null ? moon.color : Color.white;
            Color fogBase = RenderSettings.fogColor;
            Color bgBase = mainCamera != null ? mainCamera.backgroundColor : fogBase;
            float strength = close ? 1f : 0.55f;

            int flickers = Random.Range(2, 4);
            for (int i = 0; i < flickers; i++)
            {
                float k = strength * (i == 0 ? 1f : Random.Range(0.45f, 0.9f));
                Apply(k, moonBase, moonColor, fogBase, bgBase);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.09f));
                Apply(k * 0.25f, moonBase, moonColor, fogBase, bgBase);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.16f));
            }
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
            {
                Apply(strength * 0.25f * (1f - t), moonBase, moonColor, fogBase, bgBase);
                yield return null;
            }
            Apply(0f, moonBase, moonColor, fogBase, bgBase);
            Flashing = false;

            if (!thunder || AudioManager.Instance == null) yield break;
            yield return new WaitForSeconds(close ? Random.Range(0.15f, 0.5f) : Random.Range(1.2f, 3f));
            AudioManager.Instance.PlaySfx2D("thunder", close ? 0.95f : 0.6f, close ? Random.Range(0.95f, 1.05f) : Random.Range(0.78f, 0.9f));
        }

        void Apply(float k, float moonBase, Color moonColor, Color fogBase, Color bgBase)
        {
            if (moon != null)
            {
                moon.intensity = Mathf.Lerp(moonBase, 4.5f, k);
                moon.color = Color.Lerp(moonColor, flashMoon, k);
            }
            if (outsideFlash != null) outsideFlash.intensity = 2.6f * k;
            NightSky.Instance?.Flash(k);
            RenderSettings.fogColor = Color.Lerp(fogBase, flashSky, k);
            if (mainCamera != null) mainCamera.backgroundColor = Color.Lerp(bgBase, flashSky, k);
        }
    }
}
