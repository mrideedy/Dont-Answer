using UnityEngine;

namespace DontAnswer
{
    /// <summary>A bare bulb on a wire that never quite stops swinging, so every shadow in the room moves with it.</summary>
    public class SwingingLamp : MonoBehaviour
    {
        [Tooltip("Resting swing, in degrees.")]
        public float amplitude = 5f;
        public float period = 3.4f;

        float extra, phase, t;
        Quaternion rest;

        void Awake()
        {
            rest = transform.localRotation;
            phase = Random.value * 10f;
        }

        /// <summary>Something brushed past it.</summary>
        public void Nudge(float degrees) => extra = Mathf.Max(extra, degrees);

        void Update()
        {
            t += Time.deltaTime;
            extra = Mathf.MoveTowards(extra, 0f, Time.deltaTime * 2.5f);
            float w = Mathf.PI * 2f / Mathf.Max(0.5f, period);
            float a = amplitude + extra;
            transform.localRotation = rest * Quaternion.Euler(Mathf.Sin(t * w + phase) * a, 0f, Mathf.Sin(t * w * 0.71f + phase * 1.3f) * a * 0.6f);
        }
    }
}
