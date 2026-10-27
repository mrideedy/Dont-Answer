namespace DontAnswer
{
    public class PeepholeInteractable : Interactable
    {
        public override string Prompt => "[E]  Look through the peephole";
        public override void Interact() => GameDirector.Instance.EnterPeephole();
    }
}
