using UnityEngine;

namespace DontAnswer
{
    /// <summary>
    /// One fog for the whole scene, but not the same everywhere: thin inside the house (rooms stay clear and
    /// contrasty), thick outside (the yard fades into the rain, so whatever stands at the fence is a shape, not a person).
    /// Follows the camera, so the peephole and the porch get the outdoor fog.
    /// </summary>
    public class FogZones : MonoBehaviour
    {
        public float insideDensity = 0.018f;
        public float outsideDensity = 0.05f;
        [Tooltip("The house footprint, basement included. The camera inside it gets the indoor fog.")]
        public Bounds house = new Bounds(new Vector3(-2f, 0f, -3.5f), new Vector3(10.3f, 8f, 13.1f));
        public float response = 3f;

        void Start() => RenderSettings.fogDensity = insideDensity;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            float target = house.Contains(cam.transform.position) ? insideDensity : outsideDensity;
            RenderSettings.fogDensity = Mathf.MoveTowards(RenderSettings.fogDensity, target, Time.unscaledDeltaTime * response * 0.05f);
        }
    }
}
