using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace DontAnswer
{
    /// <summary>
    /// One rigged character standing in for a visitor: the model, a small playables mixer over the shared clips
    /// (time driven by hand, evaluated in LateUpdate), and the bone handles the avatar bends after animation -
    /// head turns, the stretched neck, the sitting pose. Props are primitives parented to bones.
    /// </summary>
    public class RiggedBody
    {
        // Reference points on the 1.8 m base meshes, in model space (all the Quaternius bodies share proportions).
        public const float BaseHeight = 1.8f;
        public static readonly Vector3 EyeL = new Vector3(-0.038f, 1.655f, 0.205f);
        public static readonly Vector3 EyeR = new Vector3(0.038f, 1.655f, 0.205f);
        public static readonly Vector3 Mouth = new Vector3(0f, 1.588f, 0.212f);
        const float WalkSpeed = 1.32f;   // metres per second of the walk clip at scale 1

        public Transform root, model;
        public float scale = 1f;
        public Transform head, neck, chest, armL, armR, foreArmL, foreArmR, wristL, wristR;
        public Transform legL, legR, shinL, shinR, footL, footR, legEndL, legEndR;
        Transform middleL, middleR, indexL, indexR, pinkyL, pinkyR;
        /// <summary>Something held flat on one hand (Marco's pizza box), placed after the carry pose every frame.</summary>
        public Transform carried;
        public int carrySide = -1;
        /// <summary>Something hanging from the right hand (Leo's pumpkin bucket), hung straight down under it every frame.</summary>
        public Transform hanging;
        /// <summary>How far the body sits lower than its offset (shortened legs: the feet must still reach the ground).</summary>
        public float groundDrop;
        public readonly List<Renderer> renderers = new List<Renderer>();
        public float hipHeight;
        float thighLength, shinLength, upperArmLength, foreArmLength;

        Matrix4x4 modelToHead;
        Quaternion headRestRot;
        Vector3 headRestLocal, eyeLocalL, eyeLocalR, mouthLocal, footOffsetL, footOffsetR;

        // Retargeting for bodies whose rest pose differs from the model the clips were made on (see CharacterLibrary):
        // the bones in hierarchy order, and the model-space turn that takes each from the clips' rest pose to ours.
        Transform[] fixBones;
        Quaternion[] fixTurn, evaluated;
        static readonly Dictionary<GameObject, Dictionary<string, Quaternion>> sourceRest = new Dictionary<GameObject, Dictionary<string, Quaternion>>();

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] playables;
        float[] weights, times, lengths;
        int current = -1;
        float fade = 0.25f, speed = 1f;

        public CharAnim Current => (CharAnim)Mathf.Max(0, current);
        public float WalkRate(float metresPerSecond) => metresPerSecond / (WalkSpeed * scale);
        public Vector3 HeadWorld => head != null ? head.TransformPoint((eyeLocalL + eyeLocalR) * 0.5f) : root.position + Vector3.up * 1.6f * scale;
        public Vector3 MouthWorld => head != null ? head.TransformPoint(mouthLocal) : root.position + Vector3.up * 1.55f * scale;

        // ================================================================== creation

        public static RiggedBody Create(CharacterLibrary lib, GameObject prefab, Transform parent, string name, Vector3 offset, float height, int layer)
        {
            var b = new RiggedBody();
            b.root = new GameObject(name).transform;
            b.root.gameObject.layer = layer;
            b.root.SetParent(parent, false);
            b.root.localPosition = offset;
            var go = Object.Instantiate(prefab, b.root, false);
            go.name = prefab.name;
            b.model = go.transform;
            b.scale = height / BaseHeight;
            b.model.localScale = Vector3.one * b.scale;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) b.renderers.Add(r);
            b.FindBones();
            b.MeasureRest();
            b.MatchClipSource(lib);
            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            b.BuildGraph(lib, animator);
            return b;
        }

        void FindBones()
        {
            var map = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) map[t.name] = t;
            Transform Get(string n) => map.TryGetValue(n, out var t) ? t : null;
            head = Get("Head"); neck = Get("Neck"); chest = Get("Chest");
            armL = Get("UpperArm.L"); armR = Get("UpperArm.R");
            foreArmL = Get("LowerArm.L"); foreArmR = Get("LowerArm.R");
            wristL = Get("Wrist.L"); wristR = Get("Wrist.R");
            legL = Get("UpperLeg.L"); legR = Get("UpperLeg.R");
            shinL = Get("LowerLeg.L"); shinR = Get("LowerLeg.R");
            footL = Get("Foot.L"); footR = Get("Foot.R");
            legEndL = Get("LowerLeg.L_end"); legEndR = Get("LowerLeg.R_end");
            // The finger roots all share one point, so the hand's orientation is read from the second joints.
            middleL = Get("Middle2.L"); middleR = Get("Middle2.R");
            indexL = Get("Index2.L"); indexR = Get("Index2.R");
            pinkyL = Get("Pinky2.L"); pinkyR = Get("Pinky2.R");
        }

        /// <summary>Everything measured in model space must be taken now, while the body is still in its rest pose.</summary>
        void MeasureRest()
        {
            if (head == null) return;
            modelToHead = head.worldToLocalMatrix * model.localToWorldMatrix;
            headRestRot = Quaternion.Inverse(head.rotation) * model.rotation;
            headRestLocal = head.localPosition;
            eyeLocalL = modelToHead.MultiplyPoint3x4(EyeL);
            eyeLocalR = modelToHead.MultiplyPoint3x4(EyeR);
            mouthLocal = modelToHead.MultiplyPoint3x4(Mouth);
            hipHeight = legL != null ? root.InverseTransformPoint(legL.position).y : 0.9f * scale;
            if (footL != null && legEndL != null) footOffsetL = model.InverseTransformVector(footL.position - legEndL.position);
            if (footR != null && legEndR != null) footOffsetR = model.InverseTransformVector(footR.position - legEndR.position);
            float Length(Transform a, Transform b) => a != null && b != null ? Vector3.Distance(a.position, b.position) : 0f;
            thighLength = Length(legL, shinL);
            shinLength = Length(shinL, legEndL);
            upperArmLength = Length(armL, foreArmL);
            foreArmLength = Length(foreArmL, wristL);
        }

        /// <summary>
        /// The clips were animated on one model. The women's skeleton shares its bone names but not its rest pose
        /// (thumbs a right angle out, the legs and fingers turned), so the same clip bends their hands and legs wrong.
        /// Record, per bone, how our rest pose sits relative to the source's; <see cref="Retarget"/> applies it every frame,
        /// so this body moves exactly as the source does. Must be measured in the rest pose.
        /// </summary>
        void MatchClipSource(CharacterLibrary lib)
        {
            var src = lib != null ? lib.Prefab(lib.clipSource) : null;
            if (src == null) return;
            if (!sourceRest.TryGetValue(src, out var rest))
            {
                rest = new Dictionary<string, Quaternion>();
                var r = src.transform;
                foreach (var t in src.GetComponentsInChildren<Transform>(true))
                    if (t != r) rest[PathOf(t, r)] = Quaternion.Inverse(r.rotation) * t.rotation;
                sourceRest[src] = rest;
            }
            var bones = new List<Transform>();
            var turns = new List<Quaternion>();
            float worst = 0f;
            var inv = Quaternion.Inverse(model.rotation);
            foreach (var t in model.GetComponentsInChildren<Transform>(true))   // parents before children
            {
                if (t == model || !rest.TryGetValue(PathOf(t, model), out var s)) continue;
                var turn = Quaternion.Inverse(s) * (inv * t.rotation);
                worst = Mathf.Max(worst, Quaternion.Angle(Quaternion.identity, turn));
                bones.Add(t);
                turns.Add(turn);
            }
            if (worst < 0.5f) return;   // the clips' own skeleton
            fixBones = bones.ToArray();
            fixTurn = turns.ToArray();
            evaluated = new Quaternion[fixBones.Length];
        }

        static string PathOf(Transform t, Transform root)
        {
            var path = t.name;
            for (var p = t.parent; p != null && p != root; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        /// <summary>
        /// After the clips are applied, each bone holds the source's pose. Turn every bone by its rest difference, in
        /// world space, so the mesh bends the way the source's would (parents first; all read before any are written).
        /// </summary>
        void Retarget()
        {
            if (fixBones == null) return;
            for (int i = 0; i < fixBones.Length; i++) evaluated[i] = fixBones[i].rotation;
            for (int i = 0; i < fixBones.Length; i++) fixBones[i].rotation = evaluated[i] * fixTurn[i];
        }

        void BuildGraph(CharacterLibrary lib, Animator animator)
        {
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            int n = lib.clips.Length;
            graph = PlayableGraph.Create("Visitor " + model.name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, n);
            playables = new AnimationClipPlayable[n];
            weights = new float[n];
            times = new float[n];
            lengths = new float[n];
            for (int i = 0; i < n; i++)
            {
                var clip = lib.clips[i];
                if (clip == null) continue;
                var p = AnimationClipPlayable.Create(graph, clip);
                p.SetApplyFootIK(false);
                p.SetSpeed(0);   // time is set by hand every frame
                graph.Connect(p, 0, mixer, i);
                playables[i] = p;
                lengths[i] = Mathf.Max(0.05f, clip.length);
            }
            var output = AnimationPlayableOutput.Create(graph, "Body", animator);
            output.SetSourcePlayable(mixer);
            Play(CharAnim.Idle, 0f);
            times[0] = Random.value * lengths[0];
        }

        public void Destroy()
        {
            if (graph.IsValid()) graph.Destroy();
            if (root != null) Object.Destroy(root.gameObject);
        }

        // ================================================================== animation

        static bool OneShot(int i) => i == (int)CharAnim.Death || i == (int)CharAnim.Hit;

        /// <summary>Cross-fades to a clip. Speed can be negative (walking backwards) or zero (frozen).</summary>
        public void Play(CharAnim anim, float fadeTime = 0.25f, float playSpeed = 1f, bool restart = false)
        {
            int i = (int)anim;
            if (playables == null || i >= playables.Length || !playables[i].IsValid()) i = 0;
            speed = playSpeed;
            fade = fadeTime;
            if (i == current && !restart) return;
            if (restart || OneShot(i)) times[i] = 0f;
            current = i;
            if (fadeTime <= 0f)
                for (int k = 0; k < weights.Length; k++) weights[k] = k == i ? 1f : 0f;
        }

        public void SetSpeed(float playSpeed) => speed = playSpeed;

        /// <summary>Advances the clips and writes the pose to the bones. Bend bones only after this.</summary>
        public void Tick(float dt)
        {
            if (!graph.IsValid()) return;
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (!playables[i].IsValid()) continue;
                float target = i == current ? 1f : 0f;
                weights[i] = fade <= 0f ? target : Mathf.MoveTowards(weights[i], target, dt / fade);
                sum += weights[i];
            }
            for (int i = 0; i < weights.Length; i++)
            {
                if (!playables[i].IsValid()) continue;
                float w = sum > 0f ? weights[i] / sum : (i == current ? 1f : 0f);
                mixer.SetInputWeight(i, w);
                if (w <= 0f) continue;
                times[i] += dt * (i == current ? speed : 1f);
                float time = OneShot(i) ? Mathf.Clamp(times[i], 0f, lengths[i] - 0.001f) : Mathf.Repeat(times[i], lengths[i]);
                playables[i].SetTime(time);
            }
            graph.Evaluate(0f);
            Retarget();
        }

        // ================================================================== bending bones (call after Tick)

        public static void Turn(Transform bone, Vector3 axis, float degrees)
        {
            if (bone != null && degrees != 0f) bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }

        /// <summary>Head yaw/pitch/roll in the frame of the avatar (positive pitch looks down).</summary>
        public void Look(Transform frame, float yaw, float pitch, float roll)
        {
            if (head == null) return;
            head.rotation = Quaternion.AngleAxis(yaw, frame.up) * Quaternion.AngleAxis(pitch, frame.right) *
                            Quaternion.AngleAxis(roll, frame.forward) * head.rotation;
        }

        /// <summary>The neck stretches: the head rides up and the skinned neck pulls long behind it.</summary>
        public void Stretch(Transform frame, float metres)
        {
            if (head == null || neck == null || metres <= 0f) return;
            head.position = neck.TransformPoint(headRestLocal) + frame.up * metres * scale;
        }

        /// <summary>
        /// Sitting, solved against the seat rather than bent from the idle clip (which stands some bodies with a leg
        /// crossed): knees straight ahead, shins down to the floor (or legs out on a rug), hands resting on the thighs.
        /// </summary>
        public void Sit(Transform frame, bool onFloor, float breath)
        {
            Vector3 fwd = frame.forward, right = frame.right, up = frame.up;
            Turn(chest, right, onFloor ? 7f : -3f);   // slumped back on the couch, hunched over on the floor
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = side < 0 ? legL : legR;
                var shin = side < 0 ? shinL : shinR;
                var end = side < 0 ? legEndL : legEndR;
                var foot = side < 0 ? footL : footR;
                if (hip == null || shin == null || end == null) continue;

                Vector3 thighDir = (fwd * 0.97f + right * side * 0.1f - up * (onFloor ? 0.05f : 0.09f)).normalized;
                Vector3 knee = hip.position + thighDir * thighLength;
                Vector3 shinDir = onFloor ? (fwd * 0.96f + right * side * 0.06f - up * 0.1f).normalized
                                          : (-up * 0.96f + fwd * 0.24f + right * side * 0.03f).normalized;
                Aim(hip, shin, knee);
                Aim(shin, end, shin.position + shinDir * shinLength);
                if (foot != null) foot.position = end.position + model.TransformVector(side < 0 ? footOffsetL : footOffsetR);

                var arm = side < 0 ? armL : armR;
                var fore = side < 0 ? foreArmL : foreArmR;
                var wrist = side < 0 ? wristL : wristR;
                if (arm == null || fore == null || wrist == null) continue;
                // The wrist rests on top of the thigh near the knee, palm down, fingers over the knee,
                // rising and falling with the breath. Elbows out and a little back, the way people actually sit.
                Vector3 lap = Vector3.Lerp(hip.position, shin.position, 0.7f) + up * (0.11f * scale + breath) - right * side * 0.01f * scale;
                Vector3 pole = arm.position + (right * side * 0.55f - fwd * 0.15f - up * 0.45f) * scale;
                Vector3 elbow = Elbow(arm.position, lap, upperArmLength, foreArmLength, pole);
                Aim(arm, fore, elbow);
                Aim(fore, wrist, lap);
                OrientHand(side, (fwd * 0.96f - up * 0.22f).normalized, -side * right);
            }
        }

        /// <summary>
        /// One arm carrying something flat on an upturned palm (a pizza box): upper arm down at the side,
        /// forearm level and pointing ahead. The held object is then laid on the palm.
        /// </summary>
        public void Carry(Transform frame)
        {
            int side = carrySide;
            var arm = side < 0 ? armL : armR;
            var fore = side < 0 ? foreArmL : foreArmR;
            var wrist = side < 0 ? wristL : wristR;
            if (arm == null || fore == null || wrist == null) return;
            Vector3 fwd = frame.forward, right = frame.right, up = frame.up;
            Aim(arm, fore, arm.position + (-up * 0.97f + fwd * 0.12f + right * side * 0.12f).normalized * upperArmLength);
            Aim(fore, wrist, fore.position + (fwd * 0.97f - right * side * 0.1f + up * 0.05f).normalized * foreArmLength);
            OrientHand(side, (fwd - right * side * 0.08f).normalized, side * right);   // palm up: the thumb side faces out
            if (carried != null)
                carried.SetPositionAndRotation(wrist.position + (fwd * 0.1f - right * side * 0.03f + up * 0.055f) * scale, frame.rotation);
        }

        /// <summary>Turns the hand so the fingers point along fingers and the index-finger side faces indexSide.</summary>
        void OrientHand(int side, Vector3 fingers, Vector3 indexSide)
        {
            var wrist = side < 0 ? wristL : wristR;
            var middle = side < 0 ? middleL : middleR;
            var index = side < 0 ? indexL : indexR;
            var pinky = side < 0 ? pinkyL : pinkyR;
            if (wrist == null || middle == null || index == null || pinky == null) return;
            Vector3 f = middle.position - wrist.position, a = index.position - pinky.position;
            if (f.sqrMagnitude < 1e-8f || a.sqrMagnitude < 1e-8f) return;
            var from = Quaternion.LookRotation(f, Vector3.Cross(f, a));
            var to = Quaternion.LookRotation(fingers, Vector3.Cross(fingers, indexSide));
            wrist.rotation = to * Quaternion.Inverse(from) * wrist.rotation;
        }

        /// <summary>Turns a bone so its child points at a target, keeping the bone's twist.</summary>
        static void Aim(Transform bone, Transform child, Vector3 target)
        {
            Vector3 current = child.position - bone.position, wanted = target - bone.position;
            if (current.sqrMagnitude < 1e-8f || wanted.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.FromToRotation(current, wanted) * bone.rotation;
        }

        /// <summary>Where the middle joint of a two-bone limb goes to reach a target, bending toward the pole.</summary>
        static Vector3 Elbow(Vector3 start, Vector3 target, float upper, float lower, Vector3 pole)
        {
            Vector3 toTarget = target - start;
            float d = Mathf.Clamp(toTarget.magnitude, 0.001f, upper + lower - 0.001f);
            Vector3 dir = toTarget.normalized;
            float along = (upper * upper - lower * lower + d * d) / (2f * d);
            float height = Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
            Vector3 bend = pole - start;
            bend -= Vector3.Dot(bend, dir) * dir;
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.Cross(dir, Vector3.right);
            return start + dir * along + bend.normalized * height;
        }

        /// <summary>
        /// Both palms flat against a pane of glass in front of the body, fingers up, either side of the face -
        /// the way something presses itself to a window. <paramref name="pointOnGlass"/> is any point on the pane.
        /// </summary>
        public void PressPalms(Transform frame, Vector3 pointOnGlass, float amount)
        {
            if (amount <= 0f) return;
            Vector3 fwd = frame.forward, right = frame.right, up = frame.up;
            for (int side = -1; side <= 1; side += 2)
            {
                var arm = side < 0 ? armL : armR;
                var fore = side < 0 ? foreArmL : foreArmR;
                var wrist = side < 0 ? wristL : wristR;
                if (arm == null || fore == null || wrist == null) continue;
                Quaternion a0 = arm.localRotation, f0 = fore.localRotation, w0 = wrist.localRotation;
                float toGlass = Vector3.Dot(pointOnGlass - arm.position, fwd);
                Vector3 palm = arm.position + fwd * (toGlass - 0.025f * scale) + right * side * 0.07f * scale + up * 0.2f * scale;
                Vector3 wristAt = palm - up * 0.065f * scale;
                Vector3 pole = arm.position + (right * side * 0.6f - up * 0.5f - fwd * 0.25f) * scale;
                Aim(arm, fore, Elbow(arm.position, wristAt, upperArmLength, foreArmLength, pole));
                Aim(fore, wrist, wristAt);
                OrientHand(side, up, -side * right);   // palm to the glass, thumb toward the face
                if (amount < 1f)
                {
                    arm.localRotation = Quaternion.Slerp(a0, arm.localRotation, amount);
                    fore.localRotation = Quaternion.Slerp(f0, fore.localRotation, amount);
                    wrist.localRotation = Quaternion.Slerp(w0, wrist.localRotation, amount);
                }
            }
        }

        /// <summary>Both arms come up toward you.</summary>
        public void Reach(Transform frame, float amount)
        {
            Turn(armL, frame.right, -82f * amount);
            Turn(armR, frame.right, -82f * amount);
            Turn(armL, frame.up, 10f * amount);
            Turn(armR, frame.up, -10f * amount);
        }

        public void SetShadowCasting(ShadowCastingMode mode)
        {
            foreach (var r in renderers) if (r != null) r.shadowCastingMode = mode;
        }

        // ================================================================== props

        static GameObject Prim(PrimitiveType type, int layer, Material mat, ShadowCastingMode shadow)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            go.layer = layer;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadow;
            return go;
        }

        /// <summary>Attaches a primitive to a bone, placed in model space. Only valid while the body is in its rest pose.</summary>
        public GameObject Attach(Transform bone, PrimitiveType type, Vector3 modelPos, Vector3 size, Material mat, ShadowCastingMode shadow, Vector3 euler = default)
        {
            var go = Prim(type, model.gameObject.layer, mat, shadow);
            go.transform.SetPositionAndRotation(model.TransformPoint(modelPos), model.rotation * Quaternion.Euler(euler));
            go.transform.localScale = size * scale;
            go.transform.SetParent(bone != null ? bone : model, true);
            renderers.Add(go.GetComponent<Renderer>());
            return go;
        }

        /// <summary>Attaches a primitive to the head at a model-space point of the rest pose. Valid in any pose.</summary>
        public GameObject AttachHead(PrimitiveType type, Vector3 modelPos, Vector3 size, Material mat, ShadowCastingMode shadow, Vector3 euler = default)
        {
            if (head == null) return Attach(null, type, modelPos, size, mat, shadow, euler);
            var go = Prim(type, model.gameObject.layer, mat, shadow);
            go.transform.SetParent(head, false);
            go.transform.localPosition = modelToHead.MultiplyPoint3x4(modelPos);
            go.transform.localRotation = headRestRot * Quaternion.Euler(euler);
            go.transform.localScale = size * scale / Mathf.Max(0.0001f, head.lossyScale.x);
            renderers.Add(go.GetComponent<Renderer>());
            return go;
        }
    }
}
