namespace DontAnswer
{
    /// <summary>Slam the mail slot's flap down on whatever is reaching through it.</summary>
    public class MailSlotInteractable : Interactable
    {
        public MailSlot slot;
        public override string Prompt => "[E]  Slam the mail slot on its fingers";
        public override bool CanInteract => slot != null && slot.Reaching;
        public override void Interact() => slot.Slam();
    }
}
