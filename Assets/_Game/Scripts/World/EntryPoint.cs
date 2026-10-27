using System.Collections;
using UnityEngine;

namespace DontAnswer
{
    /// <summary>A way into the apartment that a refused mimic can try: the back door or a window.</summary>
    public abstract class EntryPoint : MonoBehaviour
    {
        public string displayName = "window";
        [Tooltip("Where something stands outside while it tries this way in.")]
        public Transform outsideSpot;
        [Tooltip("Where it appears inside if it gets in.")]
        public Transform insideSpot;
        public Transform soundPoint;

        public abstract bool IsSecure { get; }
        public Vector3 SoundPosition => soundPoint != null ? soundPoint.position : transform.position;

        /// <summary>Something outside is testing this entry (rattling, tapping).</summary>
        public abstract IEnumerator Probe(VisitorDef who);

        /// <summary>It wasn't locked. It opens.</summary>
        public abstract IEnumerator ForceOpen();

        /// <summary>Snap back to a safe, closed state (used by retries).</summary>
        public abstract void ResetState(bool secure);
    }
}
