namespace DontAnswer
{
    public class WindowInteractable : Interactable
    {
        public WindowEntry window;

        bool BeingForced => window != null && window.Forcing && window.Latched;

        public override string Prompt => window == null ? null
            : BeingForced ? "[Hold E]  Shove the latch back"
            : window.Prying ? "[E]  Slam it down and latch it"
            : window.Open ? "[E]  Shut the window and latch it" : "[E]  Latch the window";

        public override bool CanInteract => window != null && (!window.IsSecure || (BeingForced && window.Force > 0.04f));
        public override float HoldDuration => BeingForced ? 0.55f : 0f;

        public override void Interact()
        {
            if (BeingForced) GameDirector.Instance.ShoveLatch(window);
            else GameDirector.Instance.SecureWindow(window);
        }
    }
}
