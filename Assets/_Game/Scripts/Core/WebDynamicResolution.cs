using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DontAnswer
{
    /// <summary>
    /// In a browser the GPU can be anything from a gaming card to a laptop's built-in chip. Once a second this checks
    /// the frame rate and sets the 3D view's resolution (URP render scale, 100% down to 50%) to whatever keeps it
    /// smooth: full resolution on a strong GPU, lower only when it has to be. Text and UI draw on top at full
    /// resolution, so they stay sharp either way. Only runs in web builds; the editor and desktop builds are untouched.
    /// </summary>
    public class WebDynamicResolution : MonoBehaviour
    {
        static readonly float[] Levels = { 1f, 0.9f, 0.8f, 0.7f, 0.6f, 0.5f };
        const float SlowFps = 45f, SmoothFps = 57f;
        const float RetryAfter = 30f;   // seconds before a resolution that proved too slow is tried again

#if UNITY_WEBGL && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject("Web Dynamic Resolution");
            DontDestroyOnLoad(go);
            go.AddComponent<WebDynamicResolution>();
        }
#endif

        UniversalRenderPipelineAsset urp;
        int level, ceiling;   // indexes into Levels: 0 is full resolution, ceiling the sharpest one allowed right now
        int smoothSeconds;
        float timer, frames, sinceTooSlow, settle = 4f;   // the first seconds are loading hitches: ignore them

        void Start()
        {
            urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) { enabled = false; return; }
            urp.renderScale = Levels[level];
        }

        void Update()
        {
            // One long frame (a shader compiling, a sound loading) shouldn't count as the game running slow.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (settle > 0f) { settle -= dt; return; }
            timer += dt;
            frames++;
            if (timer < 1f) return;
            float fps = frames / timer;
            sinceTooSlow += timer;
            timer = frames = 0f;
            // Don't bounce between two resolutions (every switch is a small hitch); try the sharper one again later.
            if (sinceTooSlow > RetryAfter) ceiling = 0;

            if (fps < SlowFps && level < Levels.Length - 1)
            {
                // Too slow: step down, two steps if it's very slow.
                ceiling = level + 1;
                sinceTooSlow = 0f;
                SetLevel(Mathf.Min(Levels.Length - 1, level + (fps < 30f ? 2 : 1)), fps);
            }
            else if (fps > SmoothFps && level > ceiling)
            {
                // Smooth for a few seconds running: try one step sharper.
                if (++smoothSeconds >= 3) SetLevel(level - 1, fps);
            }
            else smoothSeconds = 0;
        }

        void SetLevel(int newLevel, float fps)
        {
            level = newLevel;
            urp.renderScale = Levels[level];
            smoothSeconds = 0;
            settle = 1f;   // let the new resolution settle before judging it
            Debug.Log($"[Don't Answer] 3D resolution {Levels[level] * 100f:F0}% ({fps:F0} fps)");
        }
    }
}
