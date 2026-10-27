using System.Collections;
using UnityEngine;

namespace DontAnswer
{
    /// <summary>
    /// The front door: swinging open, rattling, and jolting when something pounds on it. Late at night they work the
    /// deadbolt from outside - the thumb-turn creeps round on the inside of the door - and it has to be held.
    /// </summary>
    public class FrontDoor : MonoBehaviour
    {
        public Transform hinge;
        public Transform handle;
        [Tooltip("Positive angles swing the door into the apartment.")]
        public float openAngle = 80f;
        public Transform soundPoint;
        [Tooltip("The deadbolt's thumb-turn on the inside face. Found by name under the hinge if left empty.")]
        public Transform deadbolt;

        public float Angle { get; private set; }
        public bool IsOpen => Angle > 1f;

        /// <summary>Something outside is turning the deadbolt.</summary>
        public bool Forcing { get; private set; }
        /// <summary>How far the deadbolt has been turned back, 0..1.</summary>
        public float Force { get; private set; }
        /// <summary>Counts every time the player forces the deadbolt home while it was being turned.</summary>
        public int Shoves { get; private set; }
        /// <summary>The deadbolt is back: the door is only on its latch, and it's being pushed. Slam it.</summary>
        public bool Unbolted { get; private set; }

        float bump;
        float rattleTime;
        Quaternion handleBase, boltBase;

        void Awake()
        {
            if (handle != null) handleBase = handle.localRotation;
            if (deadbolt == null && hinge != null) deadbolt = hinge.Find("Deadbolt");
            if (deadbolt != null)
            {
                boltBase = deadbolt.localRotation;
                var go = new GameObject("Deadbolt Interact");
                go.transform.SetParent(deadbolt.parent, false);
                go.transform.localPosition = deadbolt.localPosition + new Vector3(0f, 0f, -0.04f);
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(0.16f, 0.18f, 0.08f);
                go.AddComponent<DeadboltInteractable>().door = this;
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            bump = Mathf.MoveTowards(bump, 0f, dt * 5f);
            float jolt = bump > 0f ? Mathf.Sin(Time.time * 85f) * bump * 1.1f : 0f;
            hinge.localRotation = Quaternion.Euler(0f, Angle + jolt, 0f);

            if (handle != null)
            {
                float h = 0f;
                if (rattleTime > 0f)
                {
                    rattleTime -= dt;
                    h = Mathf.Sin(Time.time * 38f) * 22f + Mathf.Sin(Time.time * 61f) * 8f;
                }
                handle.localRotation = handleBase * Quaternion.Euler(0f, 0f, h);
            }
            if (deadbolt != null)
            {
                float turn = Unbolted ? 1f : Force;
                float judder = Forcing && !Unbolted ? Mathf.Sin(Time.time * 33f) * 9f * Mathf.Min(1f, Force * 4f) : 0f;
                deadbolt.localRotation = boltBase * Quaternion.Euler(0f, 0f, -88f * turn + judder);
            }
        }

        /// <summary>Jolt the door in its frame. 0.5 = a knock, 1.5 = a fist.</summary>
        public void Bump(float strength) => bump = Mathf.Max(bump, strength);

        public void RattleHandle(float duration) => rattleTime = Mathf.Max(rattleTime, duration);

        public void SetAngle(float angle) => Angle = angle;

        public IEnumerator Swing(float target, float duration)
        {
            float start = Angle;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, duration);
                Angle = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            Angle = target;
        }

        public void BeginForcing()
        {
            Forcing = true;
            Force = 0f;
        }

        public void SetForce(float amount) => Force = Mathf.Clamp01(amount);

        public void EndForcing()
        {
            Forcing = false;
            Force = 0f;
        }

        /// <summary>The player grabs the thumb-turn and forces it back.</summary>
        public void ShoveBolt()
        {
            if (!Forcing || Unbolted) return;
            Force = 0f;
            Shoves++;
            bump = Mathf.Max(bump, 1.2f);
            AudioManager.Instance.PlaySfx("deadbolt", SoundPosition, 1f, 1.1f);
        }

        /// <summary>The deadbolt turns all the way. Only the latch holds the door now.</summary>
        public void Unbolt()
        {
            Unbolted = true;
            Forcing = false;
            AudioManager.Instance.PlaySfx("deadbolt", SoundPosition, 1f, 0.8f);
        }

        /// <summary>Shut, and bolted again.</summary>
        public void Rebolt()
        {
            Unbolted = false;
            Forcing = false;
            Force = 0f;
        }

        public void ResetLock()
        {
            Unbolted = Forcing = false;
            Force = 0f;
        }

        public Vector3 SoundPosition => soundPoint != null ? soundPoint.position : transform.position;
    }
}
