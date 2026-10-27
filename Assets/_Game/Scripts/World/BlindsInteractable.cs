using UnityEngine;

namespace DontAnswer
{
    /// <summary>Part the back door's blinds with two fingers and look outside.</summary>
    public class BlindsInteractable : Interactable
    {
        public Transform viewAnchor;
        public override string Prompt => "[E]  Peek through the blinds";
        public override void Interact() => GameDirector.Instance.PeekThrough(viewAnchor, "blinds");
    }
}
