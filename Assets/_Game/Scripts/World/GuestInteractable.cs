using UnityEngine;

namespace DontAnswer
{
    /// <summary>On a guest who has been talked into opening a door for something outside: grab them before they do.</summary>
    public class GuestInteractable : Interactable
    {
        public string guestName = "them";
        public bool active;

        public override string Prompt => "[E]  Stop " + guestName + "!";
        public override bool CanInteract => active;
        public override void Interact() => GameDirector.Instance.StopGuest(this);

        public static GuestInteractable Attach(VisitorAvatar guest, string name)
        {
            var gi = guest.GetComponent<GuestInteractable>();
            if (gi == null)
            {
                var cap = guest.gameObject.AddComponent<CapsuleCollider>();
                cap.isTrigger = true;
                cap.center = new Vector3(0f, 0.85f, 0f);
                cap.height = 1.7f;
                cap.radius = 0.35f;
                gi = guest.gameObject.AddComponent<GuestInteractable>();
            }
            gi.guestName = name;
            return gi;
        }
    }
}
