using UnityEngine;

namespace DontAnswer
{
    /// <summary>Something of Leo's. Looking at it plays a memory and adds it to Sam's phone.</summary>
    public class MemoryItem : Interactable
    {
        public string memoryId = "MEM_PHOTO";
        public string title = "A photo";
        [TextArea(2, 5)] public string description;
        public string verb = "Look at";
        public bool playsSecretKnock;

        public bool Found { get; set; }

        public override string Prompt => "[E]  " + verb + " " + title.ToLowerInvariant();
        public override void Interact() => GameDirector.Instance.ShowMemory(this);
    }
}
