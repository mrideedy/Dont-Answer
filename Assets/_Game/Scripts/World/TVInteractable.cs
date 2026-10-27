namespace DontAnswer
{
    public class TVInteractable : Interactable
    {
        public TVSet tv;
        public override string Prompt => tv != null && tv.IsOn ? "[E]  Turn off the TV" : "[E]  Turn on the TV";
        public override void Interact()
        {
            if (tv != null) tv.Toggle();
        }
    }
}
