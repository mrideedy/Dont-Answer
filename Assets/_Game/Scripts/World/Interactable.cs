using UnityEngine;

namespace DontAnswer
{
    /// <summary>Anything the player can use with E / left click. Needs a collider (trigger is fine).</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public abstract string Prompt { get; }
        public virtual bool CanInteract => true;
        /// <summary>Seconds the button must be held. 0 = a single press.</summary>
        public virtual float HoldDuration => 0f;
        public abstract void Interact();
    }
}
