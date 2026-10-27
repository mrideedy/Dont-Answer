namespace DontAnswer
{
    public class NoteInteractable : Interactable
    {
        public override string Prompt => "[E]  Read the landlord's note";
        public override void Interact() => GameDirector.Instance.ReadNote();
    }
}
