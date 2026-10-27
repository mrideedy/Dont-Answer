using System.Collections;
using UnityEngine;

namespace DontAnswer
{
    /// <summary>A dark silhouette that walks past the side window, stops, looks in, and moves on.</summary>
    public class WindowFigure : MonoBehaviour
    {
        public Transform figure;
        public Transform from;
        public Transform to;
        public Transform lookTarget;
        public float walkSpeed = 1.1f;

        void Awake()
        {
            if (figure != null) figure.gameObject.SetActive(false);
        }

        public void Play()
        {
            if (figure == null || from == null || to == null) return;
            StopAllCoroutines();
            StartCoroutine(Walk());
        }

        IEnumerator Walk()
        {
            figure.gameObject.SetActive(true);
            Vector3 a = from.position, b = to.position;
            Vector3 mid = Vector3.Lerp(a, b, 0.5f);
            Quaternion along = Quaternion.LookRotation((b - a).normalized, Vector3.up);

            yield return Move(a, mid, along);

            if (lookTarget != null)
            {
                var face = lookTarget.position - figure.position; face.y = 0f;
                Quaternion look = Quaternion.LookRotation(face.normalized, Vector3.up);
                float t = 0f;
                while (t < 1f) { t += Time.deltaTime * 4f; figure.rotation = Quaternion.Slerp(along, look, t); yield return null; }
                yield return new WaitForSeconds(1.6f);
                t = 0f;
                while (t < 1f) { t += Time.deltaTime * 4f; figure.rotation = Quaternion.Slerp(look, along, t); yield return null; }
            }

            yield return Move(mid, b, along);
            figure.gameObject.SetActive(false);
        }

        IEnumerator Move(Vector3 a, Vector3 b, Quaternion rot)
        {
            float dist = Vector3.Distance(a, b);
            float t = 0f;
            figure.rotation = rot;
            while (t < 1f)
            {
                t += Time.deltaTime * walkSpeed / Mathf.Max(0.01f, dist);
                var p = Vector3.Lerp(a, b, t);
                p.y += Mathf.Abs(Mathf.Sin(t * dist * 5.5f)) * 0.04f;
                figure.position = p;
                yield return null;
            }
        }
    }
}
