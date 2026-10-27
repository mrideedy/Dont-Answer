namespace DontAnswer
{
    public class FuseBoxInteractable : Interactable
    {
        public override string Prompt => "[E]  Throw the big red breaker";
        public override bool CanInteract => GameDirector.Instance != null && GameDirector.Instance.PowerOut;
        public override void Interact() => GameDirector.Instance.ThrowBreaker();
    }
}
