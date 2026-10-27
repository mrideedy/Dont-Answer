using UnityEngine;
using UnityEngine.Rendering;

namespace DontAnswer
{
    /// <summary>
    /// Drives the "dread" post-processing volume from the sound mix: when the heartbeat rises, colour drains,
    /// the edges close in red and the image starts to tear. A night that has gone badly stays a little grey.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class MoodVolume : MonoBehaviour
    {
        [Range(0f, 1f)] public float dreadShare = 0.3f;
        public float response = 1.5f;

        Volume volume;

        void Awake()
        {
            volume = GetComponent<Volume>();
            volume.weight = 0f;
        }

        void Update()
        {
            var a = AudioManager.Instance;
            float target = a != null ? Mathf.Max(a.Tension, a.Dread * dreadShare) : 0f;
            volume.weight = Mathf.MoveTowards(volume.weight, target, Time.unscaledDeltaTime * response);
        }
    }
}
