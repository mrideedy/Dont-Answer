using UnityEngine;

namespace DontAnswer
{
    /// <summary>An ordinary swinging door inside the apartment.</summary>
    public class InteriorDoor : Interactable
    {
        public Transform hinge;
        public float openAngle = 95f;
        public bool startOpen;
        public string doorName = "door";
        public Transform soundPoint;

        public bool IsOpen { get; private set; }
        float angle, target, jolt;

        void Awake()
        {
            IsOpen = startOpen;
            angle = target = startOpen ? openAngle : 0f;
            Apply();
        }

        public override string Prompt => IsOpen ? "[E]  Close the " + doorName : "[E]  Open the " + doorName;
        public override void Interact() => SetOpen(!IsOpen, true);

        public Vector3 SoundPosition => soundPoint != null ? soundPoint.position : transform.position;

        public void SetOpen(bool open, bool withSound)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            target = open ? openAngle : 0f;
            if (withSound && AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(open ? "door_creak" : "door_slam", SoundPosition, open ? 0.45f : 0.3f, open ? 1.25f : 1.3f);
        }

        public void Jolt(float amount) => jolt = Mathf.Max(jolt, amount);

        void Update()
        {
            angle = Mathf.MoveTowards(angle, target, Time.deltaTime * 160f);
            jolt = Mathf.MoveTowards(jolt, 0f, Time.deltaTime * 5f);
            Apply();
        }

        void Apply()
        {
            if (hinge != null)
                hinge.localRotation = Quaternion.Euler(0f, angle + (jolt > 0f ? Mathf.Sin(Time.time * 80f) * jolt : 0f), 0f);
        }
    }
}
