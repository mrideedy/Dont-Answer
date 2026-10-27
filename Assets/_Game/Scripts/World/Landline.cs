using UnityEngine;

namespace DontAnswer
{
    /// <summary>The old wall phone in the kitchen. Rule 6: never answer a call from your own number.</summary>
    public class Landline : MonoBehaviour
    {
        public Renderer displayLight;
        public Transform speaker;

        public bool Ringing { get; private set; }
        public string CallerId { get; private set; } = "";
        public string CallKey { get; private set; } = "";

        AudioSource ring;
        Material lightMat;

        void Awake()
        {
            ring = gameObject.AddComponent<AudioSource>();
            ring.loop = true;
            ring.playOnAwake = false;
            ring.spatialBlend = 1f;
            ring.rolloffMode = AudioRolloffMode.Linear;
            ring.minDistance = 1.5f;
            ring.maxDistance = 22f;
            if (displayLight != null) lightMat = displayLight.material;
            SetLight(false);
        }

        public Vector3 SpeakerPosition => speaker != null ? speaker.position : transform.position;

        public void StartRinging(string callerId, string key)
        {
            CallerId = callerId;
            CallKey = key;
            Ringing = true;
            ring.clip = AudioManager.Instance.Sfx("landline_ring");
            ring.volume = 0.9f;
            if (ring.clip != null) ring.Play();
            SetLight(true);
        }

        public void StopRinging()
        {
            Ringing = false;
            ring.Stop();
            SetLight(false);
        }

        void SetLight(bool on)
        {
            if (lightMat != null) lightMat.SetColor("_BaseColor", on ? new Color(0.3f, 1.6f, 0.4f) : new Color(0.02f, 0.05f, 0.02f));
        }

        void Update()
        {
            if (Ringing && lightMat != null)
                lightMat.SetColor("_BaseColor", Mathf.Repeat(Time.time, 1f) < 0.5f ? new Color(0.3f, 1.6f, 0.4f) : new Color(0.02f, 0.05f, 0.02f));
        }
    }
}
