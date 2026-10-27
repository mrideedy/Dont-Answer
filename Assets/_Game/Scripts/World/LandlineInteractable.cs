namespace DontAnswer
{
    public class LandlineInteractable : Interactable
    {
        public Landline phone;
        public override string Prompt => phone != null && phone.Ringing ? "[E]  Answer the phone   (Caller ID: " + phone.CallerId + ")" : "";
        public override bool CanInteract => phone != null && phone.Ringing;
        public override void Interact() => GameDirector.Instance.AnswerLandline(phone);
    }
}
