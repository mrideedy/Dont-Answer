using System.Collections;
using UnityEngine;

namespace DontAnswer
{
    /// <summary>
    /// The kitchen door to the side yard. No peephole - just blinds. The bolt sticks. Late at night something
    /// slides the bolt back from outside, a little at a time, and it has to be shoved home again.
    /// </summary>
    public class BackDoor : EntryPoint
    {
        public Transform hinge;
        public Transform handle;
        public Transform bolt;
        public Vector3 boltLockedOffset = new Vector3(0.06f, 0f, 0f);
        public float openAngle = -75f;

        public bool Locked { get; private set; }
        public override bool IsSecure => Locked || (GameDirector.Instance != null && GameDirector.Instance.GuardingBackDoor);

        /// <summary>Something outside is sliding the bolt back.</summary>
        public bool Forcing { get; private set; }
        /// <summary>How far the bolt has been slid back, 0..1. At 1 it's open.</summary>
        public float Force { get; private set; }
        /// <summary>Counts every time the player shoves the bolt home while it was being forced.</summary>
        public int Shoves { get; private set; }
        /// <summary>The bolt gave and the door is being pushed open a crack. Slam it.</summary>
        public bool Ajar { get; private set; }

        float angle, targetAngle, jolt, rattle;
        Vector3 boltBase;
        Quaternion handleBase;

        void Awake()
        {
            if (bolt != null) boltBase = bolt.localPosition;
            if (handle != null) handleBase = handle.localRotation;
            displayName = "back door";
        }

        /// <summary>Someone outside tries the handle (and leans on the door).</summary>
        public void Rattle(float seconds)
        {
            rattle = Mathf.Max(rattle, seconds);
            jolt = Mathf.Max(jolt, 0.6f);
        }

        public void Jolt(float amount) => jolt = Mathf.Max(jolt, amount);

        public void SetLocked(bool locked)
        {
            Locked = locked;
            if (locked) { Ajar = false; targetAngle = 0f; Force = 0f; }
            PlaceBolt();
        }

        public override void ResetState(bool secure)
        {
            StopAllCoroutines();
            angle = targetAngle = 0f;
            Forcing = Ajar = false;
            Force = 0f;
            SetLocked(secure);
        }

        public void BeginForcing()
        {
            Forcing = true;
            Force = 0f;
        }

        public void SetForce(float amount)
        {
            Force = Mathf.Clamp01(amount);
            PlaceBolt();
        }

        public void EndForcing()
        {
            Forcing = false;
            Force = 0f;
            PlaceBolt();
        }

        /// <summary>The player throws their weight on the door and slams the bolt home.</summary>
        public void ShoveBolt()
        {
            if (!Forcing || !Locked) return;
            Force = 0f;
            Shoves++;
            jolt = 1.4f;
            PlaceBolt();
            AudioManager.Instance.PlaySfx("deadbolt", SoundPosition, 1f, 1.1f);
        }

        /// <summary>The bolt is out. The door is pushed open a hand's width, slowly.</summary>
        public void PushAjar(float degrees)
        {
            Locked = false;
            Ajar = true;
            targetAngle = Mathf.Sign(openAngle) * degrees;
            PlaceBolt();
            AudioManager.Instance.PlaySfx("door_creak", SoundPosition, 0.9f, 0.75f);
        }

        void PlaceBolt(float judder = 0f)
        {
            if (bolt == null) return;
            float shut = Locked ? 1f - Force * 0.92f : 0f;
            bolt.localPosition = boltBase + boltLockedOffset * (shut + judder);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            jolt = Mathf.MoveTowards(jolt, 0f, dt * 5f);
            rattle = Mathf.Max(0f, rattle - dt);
            angle = Mathf.MoveTowards(angle, targetAngle, dt * (Ajar ? 9f : 120f));
            float j = jolt > 0f ? Mathf.Sin(Time.time * 80f) * jolt : 0f;
            if (hinge != null) hinge.localRotation = Quaternion.Euler(0f, angle + j, 0f);
            if (handle != null)
                handle.localRotation = handleBase * Quaternion.Euler(0f, 0f, rattle > 0f || Forcing ? Mathf.Sin(Time.time * 40f) * 25f : 0f);
            if (Forcing && Locked && Force > 0.02f) PlaceBolt(Mathf.Sin(Time.time * 29f) * 0.04f);   // it judders as it's worked
        }

        public override IEnumerator Probe(VisitorDef who)
        {
            var sfx = AudioManager.Instance;
            for (int i = 0; i < 3; i++)
            {
                rattle = 1.1f;
                jolt = 0.6f;
                sfx.PlaySfx("doorknob_rattle", SoundPosition, 1f);
                yield return new WaitForSeconds(Random.Range(1.2f, 2f));
                sfx.PlaySfx(Random.value < 0.5f ? "knock_firm" : "knock_single", SoundPosition, 0.9f);
                jolt = 0.8f;
                yield return new WaitForSeconds(Random.Range(1.5f, 2.5f));
            }
        }

        public override IEnumerator ForceOpen()
        {
            Ajar = false;
            AudioManager.Instance.PlaySfx("door_creak", SoundPosition, 1f, 0.8f);
            float from = angle;
            for (float k = 0f; k < 1f; k += Time.deltaTime / 1.6f)
            {
                angle = targetAngle = Mathf.Lerp(from, openAngle, Mathf.SmoothStep(0f, 1f, k));
                yield return null;
            }
        }
    }
}
