using System.Collections;
using UnityEngine;

namespace DontAnswer
{
    /// <summary>
    /// A sash window. The wind can blow it open; an open or unlatched window is an invitation. Later in the night
    /// they work at the latch from outside - it creeps open a little at a time - and it has to be shoved back home.
    /// </summary>
    public class WindowEntry : EntryPoint
    {
        public Transform sash;
        public float openHeight = 0.42f;
        public Transform[] curtains;
        public Transform latch;
        public Collider blocker;

        public bool Latched { get; private set; } = true;
        public bool Open { get; private set; }
        public override bool IsSecure => Latched && !Open;

        /// <summary>Something outside is working the latch open.</summary>
        public bool Forcing { get; private set; }
        /// <summary>How far the latch has been worked open, 0..1. At 1 it gives.</summary>
        public float Force { get; private set; }
        /// <summary>Counts every time the player shoves the latch back while it was being forced.</summary>
        public int Shoves { get; private set; }
        /// <summary>The latch gave and the sash is being pushed up from outside, slowly. Slam it shut.</summary>
        public bool Prying { get; private set; }

        Vector3 sashBase;
        Quaternion[] curtainBase;
        float openAmount, targetOpen, shake;

        void Awake()
        {
            if (sash != null) sashBase = sash.localPosition;
            if (curtains != null)
            {
                curtainBase = new Quaternion[curtains.Length];
                for (int i = 0; i < curtains.Length; i++) curtainBase[i] = curtains[i].localRotation;
            }
        }

        public override void ResetState(bool secure)
        {
            StopAllCoroutines();
            Latched = secure;
            Open = !secure && Open;
            Forcing = Prying = false;
            Force = 0f;
            targetOpen = openAmount = Open ? 1f : 0f;
            UpdateLatch();
        }

        public void BlowOpen()
        {
            Latched = false;
            Open = true;
            targetOpen = 1f;
            UpdateLatch();
            AudioManager.Instance.PlaySfx("wind_howl", SoundPosition, 0.9f);
            AudioManager.Instance.PlaySfx("window_latch", SoundPosition, 0.8f, 0.7f);
        }

        /// <summary>Something outside touches the glass: the sash shivers in its frame.</summary>
        public void Rattle(float amount = 1f) => shake = Mathf.Max(shake, amount);

        /// <summary>Player closes and latches it.</summary>
        public void Secure()
        {
            Open = false;
            Latched = true;
            Prying = false;
            Force = 0f;
            targetOpen = 0f;
            UpdateLatch();
            AudioManager.Instance.PlaySfx("window_latch", SoundPosition, 1f);
        }

        public void BeginForcing()
        {
            Forcing = true;
            Force = 0f;
        }

        public void SetForce(float amount) => Force = Mathf.Clamp01(amount);

        public void EndForcing()
        {
            Forcing = Prying = false;
            Force = 0f;
            UpdateLatch();
        }

        /// <summary>The player leans on the latch and forces it home.</summary>
        public void ShoveLatch()
        {
            if (!Forcing || !Latched) return;
            Force = 0f;
            Shoves++;
            shake = 1.2f;
            AudioManager.Instance.PlaySfx("window_latch", SoundPosition, 1f, 1.15f);
        }

        /// <summary>The latch gives. The sash starts up, slowly, pushed from outside: a moment to slam it.</summary>
        public void PryOpen()
        {
            Latched = false;
            Open = true;
            Prying = true;
            targetOpen = 1f;
            UpdateLatch();
            AudioManager.Instance.PlaySfx("window_latch", SoundPosition, 1f, 0.7f);
        }

        void UpdateLatch()
        {
            if (latch != null) latch.localRotation = Quaternion.Euler(0f, 0f, Latched ? 0f : 70f);
            if (blocker != null) blocker.enabled = true;
        }

        void Update()
        {
            openAmount = Mathf.MoveTowards(openAmount, targetOpen, Time.deltaTime * (Prying ? 0.35f : 2.5f));
            shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 3f);
            if (sash != null)
                sash.localPosition = sashBase + Vector3.up * openHeight * openAmount + Random.insideUnitSphere * 0.004f * shake;
            if (latch != null && Forcing && Latched)
            {
                // Worked from outside: the lever creeps round, jerking back and forth.
                float jerk = Mathf.Sin(Time.time * 31f) * 7f * Mathf.Min(1f, Force * 3f);
                latch.localRotation = Quaternion.Euler(0f, 0f, Force * 58f + jerk);
            }
            if (curtains != null && curtainBase != null)
            {
                for (int i = 0; i < curtains.Length; i++)
                {
                    float wind = openAmount * (Mathf.PerlinNoise(Time.time * 1.7f, i * 3.1f) - 0.3f) * 35f;
                    curtains[i].localRotation = curtainBase[i] * Quaternion.Euler(wind, 0f, 0f);
                }
            }
        }

        public override IEnumerator Probe(VisitorDef who)
        {
            var sfx = AudioManager.Instance;
            for (int i = 0; i < 3; i++)
            {
                shake = 1f;
                sfx.PlaySfx(i == 1 ? "window_scratch" : "window_tap", SoundPosition, 1f);
                yield return new WaitForSeconds(Random.Range(1.6f, 2.6f));
            }
        }

        public override IEnumerator ForceOpen()
        {
            Open = true;
            Prying = false;
            targetOpen = 1f;
            AudioManager.Instance.PlaySfx("window_latch", SoundPosition, 1f, 0.6f);
            AudioManager.Instance.PlaySfx("wind_howl", SoundPosition, 1f);
            yield return new WaitForSeconds(1f);
        }
    }
}
