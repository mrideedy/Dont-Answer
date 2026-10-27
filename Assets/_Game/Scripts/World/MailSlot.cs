using System.Collections;
using UnityEngine;

namespace DontAnswer
{
    /// <summary>The brass mail slot in the front door. Things whisper through it. Sometimes fingers come through.</summary>
    public class MailSlot : MonoBehaviour
    {
        public Transform flap;
        public Transform fingers;
        public Transform soundPoint;

        float open;
        Vector3 fingersIn, fingersOut;
        bool slammed;

        /// <summary>Fingers are through the slot right now (and can be slammed).</summary>
        public bool Reaching { get; private set; }

        void Awake()
        {
            if (fingers != null)
            {
                fingersOut = fingers.localPosition;
                fingersIn = fingersOut + new Vector3(0f, 0f, -0.16f);
                fingers.gameObject.SetActive(false);
            }
            if (GetComponentInChildren<MailSlotInteractable>(true) == null)
            {
                // Something to aim at from inside: the flap on the inner face of the door.
                var go = new GameObject("Mail Slot Interact");
                go.transform.SetParent(transform, false);
                go.transform.position = (flap != null ? flap.position : transform.position) - transform.forward * 0.05f;
                go.transform.rotation = transform.rotation;
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(0.36f, 0.16f, 0.12f);
                go.AddComponent<MailSlotInteractable>().slot = this;
            }
        }

        public Vector3 SoundPosition => soundPoint != null ? soundPoint.position : transform.position;

        void Update()
        {
            if (flap != null) flap.localRotation = Quaternion.Euler(-70f * open, 0f, 0f);
        }

        public void ResetState()
        {
            StopAllCoroutines();
            open = 0f;
            Reaching = slammed = false;
            if (fingers != null) { fingers.localPosition = fingersOut; fingers.gameObject.SetActive(false); }
        }

        public void Slam()
        {
            if (Reaching) slammed = true;
        }

        public IEnumerator Whisper(string lineId, bool reach)
        {
            var sfx = AudioManager.Instance;
            sfx.PlaySfx("mail_slot", SoundPosition, 0.6f, 0.8f);
            for (float k = 0f; k < 1f; k += Time.deltaTime * 0.6f) { open = k * 0.8f; yield return null; }
            yield return GameDirector.Instance.SayLine(lineId, SoundPosition, true);
            if (reach && fingers != null)
            {
                fingers.gameObject.SetActive(true);
                for (float k = 0f; k < 1f; k += Time.deltaTime * 0.5f)
                {
                    fingers.localPosition = Vector3.Lerp(fingersOut, fingersIn, Mathf.SmoothStep(0f, 1f, k));
                    fingers.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 6f) * 6f, 0f, 0f);
                    yield return null;
                }
                yield return new WaitForSeconds(1.2f);
                fingers.localPosition = fingersOut;
                fingers.gameObject.SetActive(false);
            }
            open = 0f;
            sfx.PlaySfx("mail_slot", SoundPosition, 1f, 1.1f);
        }

        /// <summary>
        /// The flap lifts and long fingers come all the way through, feeling about the inside of the door - up toward
        /// the lock. They stay until the flap is slammed on them or they give up. Returns through <paramref name="result"/>
        /// whether they were slammed.
        /// </summary>
        public IEnumerator Fingers(float seconds, System.Action<bool> result)
        {
            var sfx = AudioManager.Instance;
            slammed = false;
            sfx.PlaySfx("mail_slot", SoundPosition, 0.5f, 0.75f);
            for (float k = 0f; k < 1f; k += Time.deltaTime * 0.8f) { open = k * 0.9f; yield return null; }
            if (fingers == null) { open = 0f; result?.Invoke(false); yield break; }
            fingers.gameObject.SetActive(true);
            Reaching = true;
            float t = 0f, scratch = 0.6f;
            while (t < seconds && !slammed)
            {
                t += Time.deltaTime;
                float reach = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t / 1.6f));
                // In, then feeling around: curling, sweeping side to side, lifting toward the lock.
                fingers.localPosition = Vector3.Lerp(fingersOut, fingersIn, reach)
                                        + new Vector3(Mathf.Sin(t * 1.3f) * 0.05f, Mathf.Max(0f, Mathf.Sin(t * 0.7f)) * 0.03f, 0f) * reach;
                fingers.localRotation = Quaternion.Euler(Mathf.Sin(t * 7f) * 10f - 12f * Mathf.Max(0f, Mathf.Sin(t * 0.7f)), Mathf.Sin(t * 1.3f) * 14f, Mathf.Sin(t * 5f) * 6f);
                scratch -= Time.deltaTime;
                if (scratch <= 0f)
                {
                    scratch = Random.Range(0.7f, 1.4f);
                    sfx.PlaySfx("wall_scratch", SoundPosition, 0.35f, Random.Range(1.2f, 1.5f));
                }
                yield return null;
            }
            bool wasSlammed = slammed;
            Reaching = false;
            if (wasSlammed)
            {
                // The flap comes down on them. They snatch back.
                open = 0f;
                sfx.PlaySfx("mail_slot", SoundPosition, 1f, 0.8f);
                sfx.PlaySfx("knock_heavy", SoundPosition, 0.6f, 1.3f);
                for (float k = 0f; k < 1f; k += Time.deltaTime * 6f)
                {
                    fingers.localPosition = Vector3.Lerp(fingers.localPosition, fingersOut, k);
                    yield return null;
                }
            }
            else
            {
                for (float k = 0f; k < 1f; k += Time.deltaTime * 0.9f)
                {
                    fingers.localPosition = Vector3.Lerp(fingers.localPosition, fingersOut, k);
                    yield return null;
                }
                open = 0f;
                sfx.PlaySfx("mail_slot", SoundPosition, 0.8f, 1.05f);
            }
            fingers.localPosition = fingersOut;
            fingers.gameObject.SetActive(false);
            slammed = false;
            result?.Invoke(wasSlammed);
        }
    }
}
