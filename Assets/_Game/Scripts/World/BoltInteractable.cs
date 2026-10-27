namespace DontAnswer
{
    /// <summary>The back door's sticky bolt: hold to force it shut. When it's being worked open from outside, shove it back.</summary>
    public class BoltInteractable : Interactable
    {
        public BackDoor door;

        bool BeingForced => door != null && door.Forcing && door.Locked;

        public override string Prompt => door == null ? null
            : door.Ajar ? "[E]  Slam the door and bolt it"
            : BeingForced ? "[Hold E]  Shove the bolt back"
            : "[Hold E]  Lean on the door and force the bolt shut";

        public override bool CanInteract => door != null && (!door.Locked || (BeingForced && door.Force > 0.04f));
        public override float HoldDuration => door == null || door.Ajar ? 0f : BeingForced ? 0.55f : 1.1f;

        public override void Interact()
        {
            if (door.Ajar) GameDirector.Instance.SlamBackDoor(door);
            else if (BeingForced) GameDirector.Instance.ShoveBolt(door);
            else GameDirector.Instance.LockBackDoor(door);
        }
    }
}
