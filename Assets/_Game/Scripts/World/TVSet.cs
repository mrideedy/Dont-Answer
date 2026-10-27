using UnityEngine;

namespace DontAnswer
{
    /// <summary>
    /// An old CRT. It turns itself on to static, plays the local news, and - once - shows a live
    /// picture of your own living room from outside the window.
    /// </summary>
    public class TVSet : MonoBehaviour
    {
        public enum TVMode { Off, Static, News, Feed }

        public Renderer screen;
        public Light screenLight;
        public GameObject newsOverlay;
        public Camera feedCamera;
        public RenderTexture feedTexture;

        public TVMode Mode { get; private set; } = TVMode.Off;
        public bool IsOn => Mode != TVMode.Off;

        Material mat;
        AudioSource source;
        Texture2D noise;

        void Awake()
        {
            mat = screen.material;
            noise = new Texture2D(96, 72, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "TVStatic" };
            var px = new Color32[96 * 72];
            for (int i = 0; i < px.Length; i++)
            {
                byte v = (byte)Random.Range(20, 255);
                px[i] = new Color32(v, v, (byte)Mathf.Min(255, v + 12), 255);
            }
            noise.SetPixels32(px);
            noise.Apply();

            source = gameObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = 9f;
            source.volume = 0.35f;
            SetMode(TVMode.Off);
        }

        public void Toggle() => SetMode(IsOn ? TVMode.Off : TVMode.Static);
        public void SetOn(bool on) => SetMode(on ? TVMode.Static : TVMode.Off);

        public void SetMode(TVMode m)
        {
            Mode = m;
            if (screenLight != null) screenLight.enabled = m != TVMode.Off;
            if (newsOverlay != null) newsOverlay.SetActive(m == TVMode.News);
            if (feedCamera != null) feedCamera.enabled = m == TVMode.Feed;
            switch (m)
            {
                case TVMode.Off:
                    mat.SetTexture("_BaseMap", null);
                    mat.SetColor("_BaseColor", new Color(0.02f, 0.025f, 0.03f));
                    break;
                case TVMode.Static:
                    mat.SetTexture("_BaseMap", noise);
                    mat.SetTextureScale("_BaseMap", Vector2.one);
                    mat.SetColor("_BaseColor", new Color(0.85f, 0.88f, 0.95f));
                    break;
                case TVMode.News:
                    mat.SetTexture("_BaseMap", null);
                    mat.SetColor("_BaseColor", new Color(0.05f, 0.08f, 0.2f));
                    break;
                case TVMode.Feed:
                    mat.SetTexture("_BaseMap", feedTexture);
                    mat.SetTextureScale("_BaseMap", Vector2.one);
                    mat.SetTextureOffset("_BaseMap", Vector2.zero);
                    mat.SetColor("_BaseColor", new Color(0.8f, 0.85f, 0.8f));
                    break;
            }
            source.clip = AudioManager.Instance != null ? AudioManager.Instance.Sfx("static_loop") : null;
            if (m == TVMode.Static && source.clip != null) { if (!source.isPlaying) source.Play(); }
            else source.Stop();
        }

        void Update()
        {
            if (Mode == TVMode.Static)
            {
                mat.SetTextureOffset("_BaseMap", new Vector2(Random.value, Random.value));
                if (screenLight != null) screenLight.intensity = 0.5f + Random.value * 0.45f;
            }
            else if (Mode == TVMode.Feed && screenLight != null)
            {
                screenLight.intensity = 0.35f + Random.value * 0.1f;
            }
            else if (Mode == TVMode.News && screenLight != null)
            {
                screenLight.intensity = 0.45f;
            }
        }
    }
}
