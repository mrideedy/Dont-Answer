using System;
using System.Collections.Generic;
using UnityEngine;

namespace DontAnswer
{
    public enum BodyShape { Adult, Ghost, Tall }
    public enum HatStyle { None, Cap, PoliceCap, Hood, Hair, Bun }
    public enum EyeStyle { Normal, Black, Glowing, Hollow }
    public enum MouthStyle { Normal, Wide, None }
    public enum IdleStyle { Breathe, Shiver, Sway, Nervous, Still, Twitch }
    public enum PorchLightMode { On, Flicker, Off }
    
    /// <summary>How a visitor looks. Built out of primitives by <see cref="VisitorAvatar"/>.</summary>
    [Serializable]
    public class VisitorLook
    {
        [Tooltip("Key of a rigged character prefab in Resources/Characters. Empty = build from primitives.")]
        public string modelKey = "";
        [Range(1, 3)] public int groupCount = 1;
        public BodyShape body = BodyShape.Adult;
        [Range(1f, 2.6f)] public float height = 1.75f;
        [Range(0.7f, 1.4f)] public float width = 1f;
        public Color skin = new Color(0.82f, 0.64f, 0.52f);
        public Color shirt = new Color(0.3f, 0.3f, 0.35f);
        public Color pants = new Color(0.15f, 0.15f, 0.18f);
        public Color hatColor = new Color(0.2f, 0.15f, 0.1f);
        public HatStyle hat = HatStyle.Hair;
        public EyeStyle eyes = EyeStyle.Normal;
        public MouthStyle mouth = MouthStyle.Normal;
        [Range(-60f, 60f)] public float headTilt;
        [Tooltip("Mimics cast no shadow under the porch light.")]
        public bool castsShadow = true;
        public IdleStyle idle = IdleStyle.Breathe;
        [Tooltip("Distance from the front door in metres. Closer is creepier.")]
        [Range(0.35f, 1.5f)] public float distanceFromDoor = 0.85f;
        public bool coat;
        public bool pizzaBox;
        public bool badge;
        public bool skeletonCostume;
        public bool bloodStains;
        public bool glasses;
        public bool orangeShoes;

        /// <summary>The same look for just one of them (a group of trick-or-treaters sends one to the window).</summary>
        public VisitorLook Single()
        {
            var c = JsonUtility.FromJson<VisitorLook>(JsonUtility.ToJson(this));
            c.groupCount = 1;
            return c;
        }
    }
}
