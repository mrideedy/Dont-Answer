namespace DontAnswer
{
    public class DoorInteractable : Interactable
    {
        FrontDoor door;
        FrontDoor Door => door != null ? door : door = GetComponentInParent<FrontDoor>();

        // If the deadbolt has been turned from outside and the door is being pushed in, the whole door is the thing to slam.
        public override string Prompt => Door != null && Door.Unbolted ? "[E]  Slam the door and bolt it" : "[E]  Open the door";

        public override void Interact()
        {
            if (Door != null && Door.Unbolted) GameDirector.Instance.SlamFrontDoor();
            else GameDirector.Instance.RequestOpenDoor();
        }
    }
}
