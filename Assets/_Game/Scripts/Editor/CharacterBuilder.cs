using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DontAnswer.EditorTools
{
    /// <summary>
    /// Turns the Quaternius GLBs (CC0) into game prefabs with matte URP materials, and builds the
    /// <see cref="CharacterLibrary"/> asset with the clips every body shares. Run by the scene builder.
    /// </summary>
    public static class CharacterBuilder
    {
        const string SrcDir = "Assets/_ThirdParty/Quaternius";
        const string OutDir = "Assets/_Game/Characters";
        const string MatDir = OutDir + "/Materials";
        public const string LibraryPath = "Assets/_Game/Data/Characters.asset";

        static readonly string[] Cast =
        {
            "Casual_Character", "Hoodie_Character", "Punk", "Business_Man", "Farmer", "Adventurer",
            "Hooded_Adventurer", "Animated_Woman", "Witch",
        };

        // In CharAnim order: Idle, Walk, Knock, Pound, Point, Wave, Hit, Death, Run.
        static readonly string[] ClipNames = { "Idle", "Walk", "Interact", "Punch_Right", "Idle_Gun_Pointing", "Wave", "HitRecieve", "Death", "Run" };

        [MenuItem("Don't Answer/Build Characters", priority = 2)]
        public static void BuildMenu() => Build();

        public static CharacterLibrary Build()
        {
            EnsureFolder(OutDir);
            EnsureFolder(MatDir);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var models = new List<CharacterLibrary.Model>();
            foreach (var name in Cast)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(SrcDir + "/" + name + ".glb");
                if (src == null)
                {
                    Debug.LogWarning("[Don't Answer] Missing character model " + name + ".glb - that role will use primitives.");
                    continue;
                }
                var inst = (GameObject)Object.Instantiate(src);
                inst.name = name;
                // Nobody comes to the door with a sword or a hiking pack.
                foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                    if (t != null && (t.name == "Sword" || t.name == "Backpack")) Object.DestroyImmediate(t.gameObject);
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = Convert(mats[i], shader);
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.receiveShadows = true;
                    if (r is SkinnedMeshRenderer smr)
                    {
                        smr.updateWhenOffscreen = true;
                        smr.skinnedMotionVectors = false;
                    }
                }
                var animator = inst.GetComponent<Animator>();
                if (animator == null) animator = inst.AddComponent<Animator>();
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var prefab = PrefabUtility.SaveAsPrefabAsset(inst, OutDir + "/" + name + ".prefab");
                Object.DestroyImmediate(inst);
                models.Add(new CharacterLibrary.Model { name = name, prefab = prefab });
            }

            var clips = new Dictionary<string, AnimationClip>();
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(SrcDir + "/Casual_Character.glb"))
                if (a is AnimationClip c && !c.name.StartsWith("__"))
                    clips[c.name.Substring(c.name.IndexOf('|') + 1)] = c;

            var lib = AssetDatabase.LoadAssetAtPath<CharacterLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<CharacterLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            lib.models = models.ToArray();
            lib.clipSource = ClipSource;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(OutDir + "/" + ClipSource + ".prefab");
            EnsureFolder(ClipDir);
            lib.clips = new AnimationClip[ClipNames.Length];
            for (int i = 0; i < ClipNames.Length; i++)
            {
                clips.TryGetValue(ClipNames[i], out var clip);
                if (clip == null) Debug.LogWarning("[Don't Answer] Missing character clip " + ClipNames[i]);
                else lib.clips[i] = source != null ? Complete(clip, source, ClipNames[i]) : clip;
            }
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return lib;
        }

        const string ClipSource = "Casual_Character";
        const string ClipDir = OutDir + "/Clips";

        /// <summary>
        /// A copy of the clip in which every bone of the source skeleton has a rotation: the ones the clip leaves alone
        /// are held at the source's rest pose. Bodies whose rest pose differs from the source (the women's skeleton)
        /// are then retargeted exactly at runtime (see <see cref="RiggedBody"/>), with nothing left to default values.
        /// </summary>
        static AnimationClip Complete(AnimationClip clip, GameObject source, string name)
        {
            var copy = new AnimationClip { frameRate = clip.frameRate, legacy = false };
            var animated = new HashSet<string>();
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationUtility.SetEditorCurve(copy, b, AnimationUtility.GetEditorCurve(clip, b));
                if (b.type == typeof(Transform) && b.propertyName.StartsWith("m_LocalRotation")) animated.Add(b.path);
            }
            float end = Mathf.Max(clip.length, 1f / Mathf.Max(1f, clip.frameRate));
            var root = source.transform;
            foreach (var t in source.GetComponentsInChildren<Transform>(true))
            {
                if (t == root || t.GetComponent<Renderer>() != null) continue;
                string path = AnimationUtility.CalculateTransformPath(t, root);
                if (animated.Contains(path)) continue;
                var q = t.localRotation;
                string[] axes = { "x", "y", "z", "w" };
                float[] values = { q.x, q.y, q.z, q.w };
                for (int k = 0; k < 4; k++)
                    AnimationUtility.SetEditorCurve(copy, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + axes[k]),
                        AnimationCurve.Constant(0f, end, values[k]));
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            copy.EnsureQuaternionContinuity();

            string path2 = ClipDir + "/" + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path2);
            if (existing != null)
            {
                EditorUtility.CopySerialized(copy, existing);
                Object.DestroyImmediate(copy);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            copy.name = name;
            AssetDatabase.CreateAsset(copy, path2);
            return copy;
        }

        /// <summary>One matte URP material per glTF material name and colour, shared between models.</summary>
        static Material Convert(Material src, Shader shader)
        {
            if (src == null) return null;
            Color c = src.HasProperty("baseColorFactor") ? src.GetColor("baseColorFactor")
                : src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.grey;
            Texture tex = src.HasProperty("baseColorTexture") ? src.GetTexture("baseColorTexture") : null;
            string n = src.name;
            string path = MatDir + "/" + n + "_" + ColorUtility.ToHtmlStringRGB(c) + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", c);
            m.SetTexture("_BaseMap", tex);
            bool metal = n.StartsWith("Metal") || n == "Gold" || n == "Earrings";
            float smooth = n.StartsWith("Skin") ? 0.3f : n.StartsWith("Eye") && !n.StartsWith("Eyebrow") || n == "Visor" ? 0.75f : metal ? 0.5f : 0.1f;
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal ? 0.6f : 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(m);
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
