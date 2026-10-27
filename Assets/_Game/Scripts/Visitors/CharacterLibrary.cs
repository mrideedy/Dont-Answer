using System;
using UnityEngine;

namespace DontAnswer
{
    /// <summary>The shared animations every rigged visitor can play (indices into <see cref="CharacterLibrary.clips"/>).</summary>
    public enum CharAnim { Idle, Walk, Knock, Pound, Point, Wave, Hit, Death, Run }

    /// <summary>
    /// The rigged cast (Quaternius "Ultimate Modular" characters, CC0) and the clips they share.
    /// All models use the same armature names, so one set of clips drives every body; the women's skeleton has a
    /// different rest pose, which <see cref="RiggedBody"/> retargets against the clips' source model. Built by the scene builder.
    /// </summary>
    public class CharacterLibrary : ScriptableObject
    {
        [Serializable]
        public class Model
        {
            public string name;
            public GameObject prefab;
        }

        public Model[] models = new Model[0];
        [Tooltip("Indexed by CharAnim.")]
        public AnimationClip[] clips = new AnimationClip[0];
        [Tooltip("The model the clips were animated on. Bodies with a different rest pose are corrected against it.")]
        public string clipSource = "Casual_Character";

        public GameObject Prefab(string modelName)
        {
            foreach (var m in models)
                if (m != null && m.name == modelName) return m.prefab;
            return null;
        }
    }
}
