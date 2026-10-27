namespace DontAnswer
{
    /// <summary>The front door's deadbolt: hold it while something turns it from outside; slam the door if it gives.</summary>
    public class DeadboltInteractable : Interactable
    {
        public FrontDoor door;

        bool BeingForced => door != null && door.Forcing && !door.Unbolted;

        public override string Prompt => door == null ? null
            : door.Unbolted ? "[E]  Slam the door and bolt it"
            : BeingForced ? "[Hold E]  Hold the deadbolt" : null;

        public override bool CanInteract => door != null && (door.Unbolted || (BeingForced && door.Force > 0.04f));
        public override float HoldDuration => BeingForced ? 0.55f : 0f;

        public override void Interact()
        {
            if (door.Unbolted) GameDirector.Instance.SlamFrontDoor();
            else GameDirector.Instance.ShoveDeadbolt();
        }
    }
}
