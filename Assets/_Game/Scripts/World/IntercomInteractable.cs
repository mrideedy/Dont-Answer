namespace DontAnswer
{
    public class IntercomInteractable : Interactable
    {
        public override string Prompt => "[E]  Use the intercom";
        public override void Interact() => GameDirector.Instance.UseIntercom();
    }
}
